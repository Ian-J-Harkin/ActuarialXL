using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.API.Hubs;
using ActuarialTranslationEngine.Core.Persistence;
using System.Collections.Generic;

namespace ActuarialTranslationEngine.API.Services;

public class BackgroundTranslationWorker : BackgroundService
{
    private readonly ITranslationJobQueue _jobQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTranslationWorker> _logger;

    public BackgroundTranslationWorker(
        ITranslationJobQueue jobQueue,
        IServiceProvider serviceProvider,
        ILogger<BackgroundTranslationWorker> logger)
    {
        _jobQueue = jobQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundTranslationWorker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _jobQueue.DequeueJobAsync(stoppingToken);

                _logger.LogInformation($"Processing job {job.JobId} for file {job.OriginalFileName}");

                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing translation job.");
            }
        }
    }

    private async Task ProcessJobAsync(TranslationJobRequest jobRequest, CancellationToken stoppingToken)
    {
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = jobRequest.CorrelationId });

        using var scope = _serviceProvider.CreateScope();
        
        var extractionEngine = scope.ServiceProvider.GetRequiredService<IActuarialExtractionEngine>();
        var compressionEngine = scope.ServiceProvider.GetRequiredService<IVectorCompressionEngine>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IReconciliationOrchestrator>();
        var persistenceManager = scope.ServiceProvider.GetRequiredService<IPersistenceManager>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TranslationProgressHub>>();

        // 1. Fetch existing DB Job or create it if missing
        var dbJob = await persistenceManager.GetJobDetailsAsync(jobRequest.JobId, stoppingToken);
        if (dbJob == null)
        {
            dbJob = await persistenceManager.CreateJobAsync(jobRequest.JobId, jobRequest.OriginalFileName, "uploaded-stream", "Live Model", jobRequest.TargetSheet, null, stoppingToken);
        }
        else
        {
            await persistenceManager.UpdateJobStatusAsync(jobRequest.JobId, TranslationJobStatus.Running, stoppingToken);
        }

        // 2. Link the linked cancellation token from the API if present
        CancellationTokenSource? linkedCts = null;
        var jobToken = stoppingToken;
        if (jobRequest.CancellationToken != default)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, jobRequest.CancellationToken);
            jobToken = linkedCts.Token;
        }

        var progress = new Progress<TranslationProgressEvent>(async evt => 
        {
            await hubContext.Clients.Group(dbJob.Id.ToString()).SendAsync("ProgressUpdated", evt, jobToken);
        });

        try
        {
            using var stream = new MemoryStream(jobRequest.FileData);
            
            var sheets = extractionEngine.GetWorksheetNames(stream);
            if (!sheets.Any()) throw new InvalidOperationException("No worksheets found.");

            var targetSheets = new List<string>();
            if (jobRequest.TargetSheet == "ALL")
            {
                var allowedSheets = new[] { "Table 13.4", "Example 16.3 - Part 1", "Example 18.4", "Example 13.12" };
                foreach (var s in allowedSheets)
                {
                    if (sheets.Contains(s)) targetSheets.Add(s);
                }
                if (!targetSheets.Any()) targetSheets.Add(sheets.First());
            }
            else if (sheets.Contains(jobRequest.TargetSheet))
            {
                targetSheets.Add(jobRequest.TargetSheet);
            }
            else
            {
                targetSheets.Add(sheets.First());
            }

            int partitionCounter = 0;

            foreach (var targetSheet in targetSheets)
            {
                stream.Position = 0;
                var sheetRawMap = extractionEngine.ExtractSheetData(stream, targetSheet);

                var compressedBlock = compressionEngine.CompressTopology(sheetRawMap);

                // 3. Process the block and stream the partitions directly into the DB
                await foreach (var llmOutput in orchestrator.ProcessBlockAsync(compressedBlock, sheetRawMap, progress, jobToken))
                {
                    partitionCounter++;
                    var partitionEntity = new TranslationPartitionEntity
                    {
                        JobId = dbJob.Id,
                        PartitionIndex = partitionCounter,
                        FinalAuditableMarkdown = llmOutput.FinalAuditableMarkdown,
                        GeneratedCSharpMirrorCode = llmOutput.GeneratedCSharpMirrorCode,
                        SourceName = llmOutput.SourceName,
                        IsCertified = llmOutput.IsCertified,
                        VarianceDelta = llmOutput.VarianceDelta,
                        ErrorMessage = llmOutput.ErrorMessage,
                        DisruptiveNodesJson = System.Text.Json.JsonSerializer.Serialize(llmOutput.DisruptiveNodes)
                    };
                    await persistenceManager.SavePartitionAsync(partitionEntity, jobToken);
                }
            }

            // Extract VBA logic only if 'ALL' or explicitly requested, or if we want it as a separate job
            if (jobRequest.TargetSheet == "ALL" || jobRequest.TargetSheet == "VBA")
            {
                stream.Position = 0;
                var vbaEngine = scope.ServiceProvider.GetService<IVbaExtractionEngine>();
                if (vbaEngine != null)
                {
                    var vbaModules = vbaEngine.ExtractVbaCodeStreams(stream);
                    if (vbaModules.Any())
                    {
                        var vbaOutputs = await orchestrator.ProcessVbaModulesAsync(vbaModules, jobToken);
                        foreach (var llmOutput in vbaOutputs)
                        {
                            partitionCounter++;
                            var partitionEntity = new TranslationPartitionEntity
                            {
                                JobId = dbJob.Id,
                                PartitionIndex = partitionCounter,
                                FinalAuditableMarkdown = llmOutput.FinalAuditableMarkdown,
                                GeneratedCSharpMirrorCode = llmOutput.GeneratedCSharpMirrorCode,
                                SourceName = llmOutput.SourceName,
                                IsCertified = llmOutput.IsCertified,
                                VarianceDelta = llmOutput.VarianceDelta,
                                ErrorMessage = llmOutput.ErrorMessage,
                                DisruptiveNodesJson = System.Text.Json.JsonSerializer.Serialize(llmOutput.DisruptiveNodes)
                            };
                            await persistenceManager.SavePartitionAsync(partitionEntity, jobToken);
                        }
                    }
                }
            }

            // Mark job as completed
            await persistenceManager.UpdateJobStatusAsync(dbJob.Id, TranslationJobStatus.Completed, stoppingToken);

            await hubContext.Clients.Group(dbJob.Id.ToString()).SendAsync("TranslationCompleted", new TranslationCompletedEvent
            {
                TranslationId = dbJob.Id,
                Success = true,
                Message = "Translation completed and saved successfully."
            }, stoppingToken);

            _logger.LogInformation($"Job {jobRequest.JobId} completed successfully. DB Job ID: {dbJob.Id}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Job {jobRequest.JobId} was explicitly canceled.");
            await persistenceManager.UpdateJobStatusAsync(dbJob.Id, TranslationJobStatus.Canceled, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Job {jobRequest.JobId} failed.");

            await persistenceManager.UpdateJobStatusAsync(dbJob.Id, TranslationJobStatus.Failed, stoppingToken);

            await hubContext.Clients.Group(dbJob.Id.ToString()).SendAsync("TranslationCompleted", new TranslationCompletedEvent
            {
                TranslationId = dbJob.Id,
                Success = false,
                ErrorMessage = ex.Message
            }, stoppingToken);
        }
        finally
        {
            if (Program.ActiveJobTokens.TryRemove(jobRequest.JobId, out var cts))
            {
                cts.Dispose();
            }
            linkedCts?.Dispose();
        }
    }
}
