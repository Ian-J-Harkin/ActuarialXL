using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.CLI
{
    public class CLIOrchestrator
    {
        private readonly ILogger<CLIOrchestrator> _logger;
        private readonly IActuarialExtractionEngine _extractionEngine;
        private readonly IVectorCompressionEngine _compressionEngine;
        private readonly IDomainInterrogationBridge _bridge;
        private readonly IReconciliationOrchestrator _reconciliationOrchestrator;

        public CLIOrchestrator(
            ILogger<CLIOrchestrator> logger,
            IActuarialExtractionEngine extractionEngine,
            IVectorCompressionEngine compressionEngine,
            IDomainInterrogationBridge bridge,
            IReconciliationOrchestrator reconciliationOrchestrator)
        {
            _logger = logger;
            _extractionEngine = extractionEngine;
            _compressionEngine = compressionEngine;
            _bridge = bridge;
            _reconciliationOrchestrator = reconciliationOrchestrator;
        }

        public async Task<int> RunAsync(
            string filePath,
            string? targetSheet,
            string outputDir,
            string? archetype,
            bool verbose,
            bool dryRun,
            bool e2e = false)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError($"File not found: {filePath}");
                return 2;
            }

            try
            {
                if (verbose) _logger.LogInformation($"Parsing workbook: {filePath}");

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var allSheets = _extractionEngine.GetWorksheetNames(fileStream);

                var sheetsToProcess = allSheets;

                if (!string.IsNullOrEmpty(targetSheet))
                {
                    sheetsToProcess = allSheets.Where(s => s.Equals(targetSheet, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (!sheetsToProcess.Any())
                    {
                        _logger.LogWarning($"Target sheet '{targetSheet}' not found.");
                        return 3;
                    }
                }

                if (!dryRun && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                bool hasFailures = false;
                var sheetResults = new List<(string Sheet, string Status, string? Error)>();

                foreach (var sheetName in sheetsToProcess)
                {
                    if (verbose) _logger.LogInformation($"Compressing sheet: {sheetName}");

                    try
                    {
                        // Reset stream position for each sheet extraction
                        fileStream.Position = 0;
                        var rawMap = _extractionEngine.ExtractSheetData(fileStream, sheetName);
                        var compressedBlock = _compressionEngine.CompressTopology(rawMap);

                        if (!dryRun)
                        {
                            string safeSheetName = string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars()));
                            string outPath = Path.Combine(outputDir, $"{safeSheetName}_compressed.json");
                            var json = JsonSerializer.Serialize(compressedBlock, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(outPath, json);
                            if (verbose) _logger.LogInformation($"Wrote payload to {outPath}");
                        }

                        // Phase VIII — 8-5: Full E2E LLM Translation + Roslyn Reconciliation
                        if (e2e)
                        {
                            _logger.LogInformation($"[E2E] Starting LLM translation + reconciliation for sheet: {sheetName}");

                            var cliProgress = new Progress<TranslationProgressEvent>(evt => 
                            {
                                Console.WriteLine($"[Progress] {evt.Message} ({evt.PercentComplete:F1}%)");
                            });

                            var translationResults = new List<TranslationOutput>();
                            await foreach (var result in _reconciliationOrchestrator.ProcessBlockAsync(
                                compressedBlock,
                                rawMap,
                                cliProgress,
                                CancellationToken.None))
                            {
                                translationResults.Add(result);
                                
                                if (!dryRun)
                                {
                                    string safeSheetName = string.Join("_", sheetName.Split(Path.GetInvalidFileNameChars()));
                                    string csOutPath = Path.Combine(outputDir, $"{safeSheetName}_partition_{translationResults.Count}_translated.cs");
                                    await File.WriteAllTextAsync(csOutPath, result.GeneratedCSharpMirrorCode);
                                    _logger.LogInformation($"[E2E] Wrote translated C# to {csOutPath}");
                                }
                            }

                            _logger.LogInformation($"[E2E] Reconciliation complete — {translationResults.Count} partition(s) verified.");
                        }

                        sheetResults.Add((sheetName, "Success", null));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process sheet {sheetName}");
                        sheetResults.Add((sheetName, "Failure", ex.Message));
                        hasFailures = true;
                    }
                }

                // E2E Execution Summary
                var summary = new System.Text.StringBuilder();
                summary.AppendLine("\n========================================================");
                summary.AppendLine("FINAL E2E EXECUTION SUMMARY");
                summary.AppendLine("========================================================");
                summary.AppendLine($"{"SHEET NAME",-30} | {"STATUS",-10} | {"ERROR"}");
                summary.AppendLine(new string('-', 80));
                
                foreach (var result in sheetResults)
                {
                    string statusObj = result.Status == "Success" ? "SUCCESS" : "FAILURE";
                    summary.AppendLine($"{result.Sheet,-30} | {statusObj,-10} | {result.Error}");
                }
                summary.AppendLine("========================================================");
                
                _logger.LogInformation(summary.ToString());

                return hasFailures ? 1 : 0;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal orchestration error.");
                return 1;
            }
        }
    }
}
