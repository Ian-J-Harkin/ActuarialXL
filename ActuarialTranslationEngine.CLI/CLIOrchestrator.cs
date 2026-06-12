using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.Core.Interfaces;

namespace ActuarialTranslationEngine.CLI
{
    public class CLIOrchestrator
    {
        private readonly ILogger<CLIOrchestrator> _logger;
        private readonly IActuarialExtractionEngine _extractionEngine;
        private readonly IVectorCompressionEngine _compressionEngine;

        public CLIOrchestrator(
            ILogger<CLIOrchestrator> logger,
            IActuarialExtractionEngine extractionEngine,
            IVectorCompressionEngine compressionEngine)
        {
            _logger = logger;
            _extractionEngine = extractionEngine;
            _compressionEngine = compressionEngine;
        }

        public async Task<int> RunAsync(string filePath, string? targetSheet, string outputDir, string? archetype, bool verbose, bool dryRun)
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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process sheet {sheetName}");
                        hasFailures = true;
                    }
                }

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
