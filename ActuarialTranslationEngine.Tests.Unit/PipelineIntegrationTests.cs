using System.IO;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Engine;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit;

public class PipelineIntegrationTests
{
    private const string TargetFile = @"C:\Github\ActuarialXLpoc\edu-2012-c13-01.xlsx";

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PipelineIntegrationTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EndToEndPipeline_ExtractCompressAndInterrogate_Table13_4()
    {
        // 1. Arrange Engines
        var extractionEngine = new ActuarialExtractionEngine();
        var compressionEngine = new VectorCompressionEngine();
        var mockBridge = new MockDomainInterrogationBridge();

        // 2. Act: Execute Pipeline
        using var fileStream = new FileStream(TargetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        // Phase II.2: Extraction
        var rawMap = extractionEngine.ExtractSheetData(fileStream, "Table 13.4");
        
        // Phase II.3: Compression
        var compressedBlock = compressionEngine.CompressTopology(rawMap);
        
        // Phase II.4: Interrogation (Mocked LLM Bridge)
        var finalTranslation = await mockBridge.ProcessPayloadAsync(compressedBlock, "G");

        // 3. Assert End-to-End Correctness
        Assert.NotNull(rawMap);
        Assert.Equal("Table 13.4", rawMap.SheetName);
        Assert.Equal(60, rawMap.DataRows.Count); // 60 recursive body rows

        foreach(var p in compressedBlock.Partitions)
        {
            _output.WriteLine($"Partition: Rows {p.StartRow}-{p.EndRow} | Signature: {p.FormulaSignature}");
        }

        Assert.NotNull(compressedBlock);
        // Empirical End-to-End Discovery:
        // The real spreadsheet changes the static mortality rate in Column I five times across the 60 rows!
        // Also, Column A is empty on Row 6, but uses +A6+1 recursively thereafter.
        // Therefore, the mathematically correct partition count is 6, not 2!
        Assert.Equal(6, compressedBlock.Partitions.Count);

        Assert.NotNull(finalTranslation);
        // The mock bridge embeds the valid C# and Markdown outputs, PLUS the dynamic partition count validation
        Assert.Contains("public class DynamicReconciliationUnit", finalTranslation.GeneratedCSharpMirrorCode);
        Assert.Contains("Partitions: 6", finalTranslation.FinalAuditableMarkdown);
    }
    [Fact]
    public void EndToEndPipeline_ExtractCompressAndInterrogate_StochasticModeling()
    {
        var extractionEngine = new ActuarialExtractionEngine();
        var compressionEngine = new VectorCompressionEngine();
        using var fileStream = new FileStream(TargetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        var rawMap = extractionEngine.ExtractSheetData(fileStream, "Example 16.3 - Part 1");
        var compressedBlock = compressionEngine.CompressTopology(rawMap);
        
        Assert.NotNull(rawMap);
        Assert.Equal("Example 16.3 - Part 1", rawMap.SheetName);
        Assert.NotNull(compressedBlock);
        
        _output.WriteLine($"StochasticModeling Partitions: {compressedBlock.Partitions.Count}");
        Assert.True(compressedBlock.Partitions.Count > 0);
    }

    [Fact]
    public void EndToEndPipeline_ExtractCompressAndInterrogate_BalancingLedgers()
    {
        var extractionEngine = new ActuarialExtractionEngine();
        var compressionEngine = new VectorCompressionEngine();
        using var fileStream = new FileStream(TargetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        var rawMap = extractionEngine.ExtractSheetData(fileStream, "Solution to Exercise 18.4");
        var compressedBlock = compressionEngine.CompressTopology(rawMap);
        
        Assert.NotNull(rawMap);
        Assert.Equal("Solution to Exercise 18.4", rawMap.SheetName);
        Assert.NotNull(compressedBlock);
        
        _output.WriteLine($"BalancingLedgers Partitions: {compressedBlock.Partitions.Count}");
        Assert.True(compressedBlock.Partitions.Count > 0);
    }

    [Fact]
    public void EndToEndPipeline_ExtractCompressAndInterrogate_VariableAdjusters()
    {
        var extractionEngine = new ActuarialExtractionEngine();
        var compressionEngine = new VectorCompressionEngine();
        using var fileStream = new FileStream(TargetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        var rawMap = extractionEngine.ExtractSheetData(fileStream, "Example 13.12");
        var compressedBlock = compressionEngine.CompressTopology(rawMap);
        
        Assert.NotNull(rawMap);
        Assert.Equal("Example 13.12", rawMap.SheetName);
        Assert.NotNull(compressedBlock);
        
        _output.WriteLine($"VariableAdjusters Partitions: {compressedBlock.Partitions.Count}");
        Assert.True(compressedBlock.Partitions.Count > 0);
    }
}
