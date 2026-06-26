using System.IO;
using System.Linq;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Core.Exceptions;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit;

public class ActuarialExtractionEngineTests
{
    private readonly string _testFilePath;

    public ActuarialExtractionEngineTests()
    {
        // Assuming the tests are run from the solution root or bin folder, pointing back to the Excel file
        // in the root of the repo.
        string basePath = Directory.GetCurrentDirectory();
        // Walk up to find the root folder containing the excel file
        while (!File.Exists(Path.Combine(basePath, "edu-2012-c13-01.xlsx")) && basePath.Length > 3)
        {
            basePath = Directory.GetParent(basePath)?.FullName ?? basePath;
        }
        _testFilePath = Path.Combine(basePath, "edu-2012-c13-01.xlsx");
    }

    [Fact]
    public void ExtractSheetData_ThrowsArgumentNullException_IfStreamIsNull()
    {
        var engine = new ActuarialExtractionEngine();
        Assert.Throws<System.ArgumentNullException>(() => engine.ExtractSheetData(null!, "Sheet1"));
    }

    [Fact]
    public void ExtractSheetData_ThrowsExtractionException_IfFileIsInvalidFormat()
    {
        var engine = new ActuarialExtractionEngine();
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03 }); // Not an Excel file
        var ex = Assert.Throws<ActuarialExtractionException>(() => engine.ExtractSheetData(stream, "Sheet1"));
        Assert.Contains("not a valid Excel package", ex.Message);
    }

    [Fact]
    public void ExtractSheetData_ThrowsExtractionException_IfSheetNotFound()
    {
        var engine = new ActuarialExtractionEngine();
        using var stream = File.OpenRead(_testFilePath);

        var ex = Assert.Throws<ActuarialExtractionException>(() => engine.ExtractSheetData(stream, "NonExistentSheet"));
        Assert.Contains("Sheet 'NonExistentSheet' not found", ex.Message);
    }

    [Fact]
    public void ExtractSheetData_Table13_4_ProperlyExtractsHeadersAndData()
    {
        var engine = new ActuarialExtractionEngine();
        using var stream = File.OpenRead(_testFilePath);

        var result = engine.ExtractSheetData(stream, "Table 13.4");

        // Assert 1: Identity preserved
        Assert.Equal("Table 13.4", result.SheetName);

        // Assert 2: Headers detected on Row 4, exactly 14 columns
        string allHeaders = string.Join(", ", result.Headers.Select(h => h.ExtractedHeaderName));
        Assert.True(14 == result.Headers.Count, $"Expected 14 headers, but got {result.Headers.Count}. Headers were: {allHeaders}");
        Assert.Equal("Policy Month", result.Headers.First().ExtractedHeaderName);
        Assert.Equal("Notes", result.Headers.Last().ExtractedHeaderName);

        // Assert 3: Data row bounds
        Assert.Equal(60, result.DataRows.Count);
        Assert.Equal(6, result.DataRows.First().RowIndex);
        Assert.Equal(65, result.DataRows.Last().RowIndex);

        // Assert 4: Formula extraction
        // Row 6 Column M ("Fund Value End of Month") should be +K6+L6
        var row6 = result.DataRows.Single(r => r.RowIndex == 6);
        Assert.True(row6.CellFormulas.ContainsKey("M"));
        Assert.Equal("+K6+L6", row6.CellFormulas["M"]); 
    }
}
