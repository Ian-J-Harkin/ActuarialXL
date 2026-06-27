using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.API.Services;

namespace ActuarialTranslationEngine.API.Endpoints;

public static class EvaluateEndpoints
{
    public static void MapEvaluateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/evaluate", async (HttpRequest request, 
            ITranslationJobQueue jobQueue,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("EvaluateEndpoint");
            try
            {
                if (!request.HasFormContentType || !request.Form.Files.Any())
                {
                    return Results.BadRequest(new { Error = "No file uploaded." });
                }

                var file = request.Form.Files.First();
                var fileName = file.FileName;
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                
                if (ext != ".xlsx" && ext != ".xlsm")
                {
                    return Results.BadRequest(new { Error = "Unsupported file type. Only .xlsx and .xlsm are supported." });
                }

                // Security: 5MB File Limit
                if (file.Length > 5 * 1024 * 1024)
                {
                    return Results.BadRequest(new { Error = "File exceeds the 5MB upload limit." });
                }

                // Write to disk instead of memory to prevent OOM
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                var jobId = Guid.NewGuid();
                var filePath = Path.Combine(uploadDir, $"{jobId}.xlsx");
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream, cancellationToken);
                }
                
                // Security: Magic Byte Check for ZIP/XLSX (PK = 50 4B)
                byte[] magicBytes = new byte[2];
                using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    readStream.Read(magicBytes, 0, 2);
                }
                
                if (magicBytes[0] != 0x50 || magicBytes[1] != 0x4B)
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    return Results.BadRequest(new { Error = "Invalid file signature. File is not a valid ZIP/XLSX." });
                }

                string? connectionId = request.Form.TryGetValue("connectionId", out var cid) ? cid.ToString() : null;
                string correlationId = request.Form.TryGetValue("correlationId", out var corid) ? corid.ToString() : Guid.NewGuid().ToString();
                string targetSheet = request.Form.TryGetValue("targetSheet", out var tSheet) ? tSheet.ToString() : "ALL";

                using var logScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

                // Create a dedicated CTS for this job
                var cts = new CancellationTokenSource();
                Program.ActiveJobTokens.TryAdd(jobId, cts);

                var jobRequest = new TranslationJobRequest
                {
                    JobId = jobId,
                    OriginalFileName = fileName,
                    FilePath = filePath,
                    TargetSheet = targetSheet,
                    ConnectionId = connectionId,
                    CorrelationId = correlationId,
                    CancellationToken = cts.Token
                };

                await jobQueue.EnqueueJobAsync(jobRequest, cancellationToken);

                logger.LogInformation($"Job {jobId} enqueued for file {fileName}. ConnectionId: {connectionId}");

                return Results.Accepted($"/api/history/{jobId}", new { JobId = jobId, Status = "Accepted" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enqueue evaluation job.");
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapDelete("/api/evaluate/{id:guid}", (Guid id, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EvaluateEndpoint");
            if (Program.ActiveJobTokens.TryRemove(id, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                logger.LogInformation($"Cancellation requested for Job {id}");
                return Results.Ok(new { Message = "Job cancellation requested." });
            }
            return Results.NotFound(new { Error = "Active job not found or already completed." });
        });
    }
}
