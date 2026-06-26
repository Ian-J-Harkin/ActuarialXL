namespace ActuarialTranslationEngine.Engine.Orchestration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using Microsoft.Extensions.Logging;

public class ReconciliationOrchestrator : IReconciliationOrchestrator
{
    private readonly IDomainInterrogationBridge _bridge;
    private readonly IRoslynReconciliationEngine _roslynEngine;
    private readonly ILogger<ReconciliationOrchestrator> _logger;

    public ReconciliationOrchestrator(IDomainInterrogationBridge bridge, IRoslynReconciliationEngine roslynEngine, ILogger<ReconciliationOrchestrator> logger)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _roslynEngine = roslynEngine ?? throw new ArgumentNullException(nameof(roslynEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<TranslationOutput> ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap workbookMap, IProgress<TranslationProgressEvent>? progress = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (block == null) throw new ArgumentNullException(nameof(block));
        if (workbookMap == null) throw new ArgumentNullException(nameof(workbookMap));
        if (block.Partitions == null || block.Partitions.Count == 0) yield break;

        cancellationToken.ThrowIfCancellationRequested();

        // Use a SemaphoreSlim to bound concurrency across partitions to 3
        var semaphore = new SemaphoreSlim(3);
        
        var tasks = block.Partitions.Select(async (partition, index) => 
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                int partitionIndex = index + 1;
                progress?.Report(new TranslationProgressEvent 
                { 
                    CurrentPartition = partitionIndex, 
                    TotalPartitions = block.Partitions.Count, 
                    Message = $"Starting translation for partition {partition.StartRow}-{partition.EndRow}..." 
                });
                cancellationToken.ThrowIfCancellationRequested();

                string targetColumn = DetermineTargetColumn(partition);
                var sequentialRows = workbookMap.DataRows
                    .Where(r => r.RowIndex >= partition.StartRow && r.RowIndex <= partition.EndRow
                                && !r.DisruptiveNodes.Any())
                    .OrderBy(r => r.RowIndex)
                    .ToList();

                if (!sequentialRows.Any())
                {
                    return new TranslationOutput
                    {
                        SourceName = $"Worksheet: {block.TargetWorksheet} (Rows {partition.StartRow}-{partition.EndRow})",
                        FinalAuditableMarkdown = $"Partition could not be translated due to anomalies:\n" + string.Join("\n", block.DisruptiveNodes.Select(n => $"- {n.Coordinate}: {n.ExceptionFlag}")),
                        GeneratedCSharpMirrorCode = $"public class FailedPartition_{partition.StartRow} {{ /* Partition {partition.StartRow}-{partition.EndRow} has no structurally clean rows to validate against. */ }}",
                        IsCertified = false,
                        ErrorMessage = $"Partition {partition.StartRow}-{partition.EndRow} has no structurally clean rows to validate against.",
                        DisruptiveNodes = block.DisruptiveNodes
                    };
                }

                return await RequestTranslationWithRetryAsync(block, partition, targetColumn, sequentialRows, workbookMap, partitionIndex, progress, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        while (tasks.Any())
        {
            var finishedTask = await Task.WhenAny(tasks);
            tasks.Remove(finishedTask);
            yield return await finishedTask;
        }
    }

    private async Task<TranslationOutput> RequestTranslationWithRetryAsync(CompressedVectorBlock block, VectorRangePartition partition, string targetColumn, List<RawRowMetadata> sequentialRows, RawWorkbookMap workbookMap, int partitionIndex, IProgress<TranslationProgressEvent>? progress, CancellationToken cancellationToken)
    {
        string? previousError = null;
        TranslationOutput? lastOutput = null;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var msg = $"[Attempt {attempt}] Requesting LLM translation for partition {partition.StartRow}-{partition.EndRow}...";
                _logger.LogInformation(msg);
                progress?.Report(new TranslationProgressEvent 
                { 
                    CurrentPartition = partitionIndex, 
                    TotalPartitions = block.Partitions.Count, 
                    Message = msg 
                });
                
                var isolatedPayload = new CompressedVectorBlock
                {
                    TargetWorksheet = block.TargetWorksheet,
                    ProcessingArchetype = block.ProcessingArchetype,
                    Partitions = new List<VectorRangePartition> { partition },
                    DisruptiveNodes = block.DisruptiveNodes
                };
                
                var llmOutputRaw = await _bridge.ProcessPayloadAsync(isolatedPayload, targetColumn, previousError, cancellationToken);
                var llmOutput = new TranslationOutput
                {
                    SourceName = $"Worksheet: {block.TargetWorksheet} (Rows {partition.StartRow}-{partition.EndRow})",
                    FinalAuditableMarkdown = llmOutputRaw.FinalAuditableMarkdown,
                    GeneratedCSharpMirrorCode = llmOutputRaw.GeneratedCSharpMirrorCode,
                    IsCertified = true,
                    DisruptiveNodes = block.DisruptiveNodes
                };
                lastOutput = llmOutput;
                var msgReceived = $"[Attempt {attempt}] LLM translation received. Verifying {sequentialRows.Count} rows...";
                _logger.LogInformation(msgReceived);
                progress?.Report(new TranslationProgressEvent 
                { 
                    CurrentPartition = partitionIndex, 
                    TotalPartitions = block.Partitions.Count, 
                    Message = msgReceived 
                });
                
                await VerifyPartitionRowsAsync(llmOutput.GeneratedCSharpMirrorCode, partition, targetColumn, sequentialRows, workbookMap, attempt, cancellationToken);
                return llmOutput;
            }
            catch (ActuarialDynamicCompilationException ex)
            {
                previousError = string.Join("\n", ex.Diagnostics.Select(d => d.GetMessage()));
                if (attempt == 3) break;
            }
            catch (ActuarialLogicLeakException ex)
            {
                if (attempt == 3)
                {
                    if (lastOutput != null)
                    {
                        lastOutput = new TranslationOutput
                        {
                            SourceName = lastOutput.SourceName,
                            FinalAuditableMarkdown = lastOutput.FinalAuditableMarkdown,
                            GeneratedCSharpMirrorCode = lastOutput.GeneratedCSharpMirrorCode,
                            IsCertified = false,
                            VarianceDelta = ex.Variance,
                            ErrorMessage = ex.Message,
                            DisruptiveNodes = block.DisruptiveNodes
                        };
                    }
                    break;
                }
                previousError = ex.Message;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // This is a timeout, not a user-initiated cancellation. Retry.
                _logger.LogWarning($"[Attempt {attempt}] LLM call timed out for partition {partition.StartRow}-{partition.EndRow}. Retrying...");
                previousError = "The previous request timed out. Please generate the code more concisely.";
                if (attempt == 3) break;
            }
        }

        if (lastOutput != null)
        {
            if (lastOutput.IsCertified)
            {
                return new TranslationOutput
                {
                    SourceName = lastOutput.SourceName,
                    FinalAuditableMarkdown = lastOutput.FinalAuditableMarkdown,
                    GeneratedCSharpMirrorCode = lastOutput.GeneratedCSharpMirrorCode,
                    IsCertified = false,
                    ErrorMessage = previousError ?? "Failed to compile valid C# after 3 attempts.",
                    DisruptiveNodes = block.DisruptiveNodes
                };
            }
            return lastOutput;
        }

        return new TranslationOutput
        {
            SourceName = $"Worksheet: {block.TargetWorksheet} (Rows {partition.StartRow}-{partition.EndRow})",
            FinalAuditableMarkdown = "Translation failed completely.",
            GeneratedCSharpMirrorCode = "// No code generated",
            IsCertified = false,
            ErrorMessage = "Failed to communicate with LLM or failed completely.",
            DisruptiveNodes = block.DisruptiveNodes
        };
    }

    private async Task VerifyPartitionRowsAsync(string csharpCode, VectorRangePartition partition, string targetColumn, List<RawRowMetadata> sequentialRows, RawWorkbookMap workbookMap, int attempt, CancellationToken cancellationToken)
    {
        var runningState = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation($"[Attempt {attempt}] Verifying {sequentialRows.Count} rows for partition {partition.StartRow}-{partition.EndRow}...");
        int rowCounter = 0;
        foreach (var rowData in sequentialRows)
        {
            rowCounter++;
            if (rowCounter % 10 == 0 || rowCounter == 1)
            {
                _logger.LogInformation($"    -> Evaluating row {rowData.RowIndex} ({rowCounter}/{sequentialRows.Count})...");
            }

            cancellationToken.ThrowIfCancellationRequested();

            decimal expectedResult = 0m;

            // 1. Inject safe defaults for all structural columns for all required offsets
            var requiredOffsets = new HashSet<int>();
            foreach (var col in partition.StructuralColumns)
            {
                if (!runningState.ContainsKey(col.ColumnLetter))
                    runningState[col.ColumnLetter] = 0m;
                    
                foreach (var offset in col.ChronologicalLookbacks)
                {
                    requiredOffsets.Add(offset);
                    string sign = offset > 0 ? "+" : "";
                    runningState[$"{col.ColumnLetter}[{sign}{offset}]"] = 0m;
                }
            }

            // 2. Inject historical and future row context dynamically based on required offsets
            foreach (var offset in requiredOffsets)
            {
                var offsetRow = workbookMap.DataRows.FirstOrDefault(r => r.RowIndex == rowData.RowIndex + offset);
                if (offsetRow != null)
                {
                    string sign = offset > 0 ? "+" : "";
                    foreach (var cell in offsetRow.CellValues)
                    {
                        if (decimal.TryParse(cell.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pv))
                        {
                            runningState[$"{cell.Key}[{sign}{offset}]"] = pv;
                        }
                    }
                }
            }

            // 3. Load pristine Excel values for this row into the running state.
            foreach (var cell in rowData.CellValues)
            {
                if (decimal.TryParse(cell.Value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsedValue))
                {
                    if (cell.Key == targetColumn)
                    {
                        expectedResult = parsedValue;
                    }
                    else
                    {
                        runningState[cell.Key] = parsedValue;
                    }
                }
                else
                {
                    _logger.LogInformation($"[Row {rowData.RowIndex}] Could not parse decimal from column '{cell.Key}' with value '{cell.Value}'");
                }
            }

            // 4. Ensure any keys referenced by the LLM code exist in the dictionary as 0m if not already parsed
            var keyRegex = new System.Text.RegularExpressions.Regex(@"inputs\[""([^""]+)""\]");
            foreach (System.Text.RegularExpressions.Match match in keyRegex.Matches(csharpCode))
            {
                var key = match.Groups[1].Value;
                if (!runningState.ContainsKey(key))
                {
                    runningState[key] = 0m;
                }
            }

            if (rowCounter == 1) 
            {
                _logger.LogInformation($"[Attempt {attempt}] LLM generated C# code:\n{csharpCode}");
            }
            
            // C. Compile and Execute against the stateful dictionary.
            await _roslynEngine.CompileAndVerifyAsync(csharpCode, runningState, expectedResult, cancellationToken);

            // D. Ground-Truth Reset (Phase VIII - 8-3):
            runningState[targetColumn] = expectedResult;
        }
    }

    public async Task<List<TranslationOutput>> ProcessVbaModulesAsync(List<VbaModuleCode> modules, CancellationToken cancellationToken = default)
    {
        var results = new List<TranslationOutput>();
        foreach (var module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            string? previousError = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    _logger.LogInformation($"[Attempt {attempt}] Requesting LLM translation for VBA module {module.ModuleName}...");
                    var llmOutputRaw = await _bridge.ProcessVbaPayloadAsync(module, previousError, cancellationToken);
                    var llmOutput = new TranslationOutput
                    {
                        SourceName = $"VBA Module: {module.ModuleName}",
                        FinalAuditableMarkdown = llmOutputRaw.FinalAuditableMarkdown,
                        GeneratedCSharpMirrorCode = llmOutputRaw.GeneratedCSharpMirrorCode
                    };
                    _logger.LogInformation($"[Attempt {attempt}] VBA module {module.ModuleName} translation received.");
                    
                    var emptyInputs = new Dictionary<string, decimal>();
                    await _roslynEngine.CompileAndVerifyAsync(llmOutput.GeneratedCSharpMirrorCode, emptyInputs, 0m, cancellationToken);

                    results.Add(llmOutput);
                    break;
                }
                catch (ActuarialDynamicCompilationException ex)
                {
                    if (attempt == 3) throw;
                    previousError = string.Join("\n", ex.Diagnostics.Select(d => d.GetMessage()));
                }
                catch (ActuarialLogicLeakException)
                {
                    // Ignore variance failures (penny-matching) for VBA since we use a mock 0m target.
                    // The primary goal for VBA modules is successful syntax compilation.
                    break;
                }
            }
        }
        
        return results;
    }

    private string DetermineTargetColumn(VectorRangePartition partition)
    {
        var keywords = new[] { "Total", "Net", "Reserve", "Balance" };
        foreach (var col in partition.StructuralColumns)
        {
            if (keywords.Any(k => col.ExtractedHeaderName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return col.ColumnLetter;
            }
        }

        return partition.StructuralColumns.Last().ColumnLetter;
    }
}
