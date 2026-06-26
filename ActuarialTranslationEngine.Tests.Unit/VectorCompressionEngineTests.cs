using System.Collections.Generic;
using System.Linq;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit;

public class VectorCompressionEngineTests
{
    [Fact]
    public void CompressTopology_ThrowsArgumentNullException_IfMapIsNull()
    {
        var engine = new VectorCompressionEngine();
        Assert.Throws<System.ArgumentNullException>(() => engine.CompressTopology(null!));
    }

    [Fact]
    public void CompressTopology_ReturnsEmptyBlock_IfDataRowsIsEmpty()
    {
        var engine = new VectorCompressionEngine();
        var map = new RawWorkbookMap { SheetName = "EmptySheet" }; // DataRows is inherently empty
        var result = engine.CompressTopology(map);
        Assert.Equal("EmptySheet", result.TargetWorksheet);
        Assert.Empty(result.Partitions);
    }

    [Fact]
    public void CompressTopology_Table13_4_ProperlySegmentsPartitions()
    {
        // Arrange
        var engine = new VectorCompressionEngine();
        var map = CreateMockTable13_4Map();

        // Act
        var result = engine.CompressTopology(map);

        // Assert 1: Sheet identity preserved
        Assert.Equal("Table 13.4", result.TargetWorksheet);

        // Assert 2: Partition count: 2 (body-era-1 + body-era-2 due to Row 54 mortality shift)
        Assert.InRange(result.Partitions.Count, 2, 3);
        Assert.Equal(2, result.Partitions.Count);

        // Assert 3: The recursive body partition must span rows 6 to 65
        // Wait, the partitions will be:
        // P1: Rows 6 to 53
        // P2: Rows 54 to 65
        var p1 = result.Partitions[0];
        Assert.Equal(6, p1.StartRow);
        Assert.Equal(53, p1.EndRow);

        var p2 = result.Partitions[1];
        Assert.Equal(54, p2.StartRow);
        Assert.Equal(65, p2.EndRow);

        // Assert 4: Formula signature stability
        // Checked inherently by the fact that it partitioned into exactly 2 ranges.

        // Assert 5: Column count: 14 columns (A through N)
        Assert.Equal(14, p1.StructuralColumns.Count);
        
        // Assert 6: Tokenized formula correctness
        // Let's check Column M (Fund Value End of Month) on P1
        var colM = p1.StructuralColumns.Single(c => c.ColumnLetter == "M");
        // The mock creates "+K6+L6" on row 6, which abstracts to "Col[K]+Col[L]"
        Assert.Equal("+Col[K]+Col[L]", colM.TokenizedFormulaTemplate);
    }

    private RawWorkbookMap CreateMockTable13_4Map()
    {
        var headers = new List<ColumnDefinition>
        {
            new ColumnDefinition { ColumnLetter = "A", ExtractedHeaderName = "Policy Month" },
            new ColumnDefinition { ColumnLetter = "B", ExtractedHeaderName = "Premium" },
            new ColumnDefinition { ColumnLetter = "C", ExtractedHeaderName = "Premium Expense Charge" },
            new ColumnDefinition { ColumnLetter = "D", ExtractedHeaderName = "Net Premium" },
            new ColumnDefinition { ColumnLetter = "E", ExtractedHeaderName = "Per policy Expense Charge" },
            new ColumnDefinition { ColumnLetter = "F", ExtractedHeaderName = "Per 1000 Expense Charge" },
            new ColumnDefinition { ColumnLetter = "G", ExtractedHeaderName = "Death Benefit" },
            new ColumnDefinition { ColumnLetter = "H", ExtractedHeaderName = "Net Amount at Risk" },
            new ColumnDefinition { ColumnLetter = "I", ExtractedHeaderName = "Cost of Insurance Rate Per 1000" },
            new ColumnDefinition { ColumnLetter = "J", ExtractedHeaderName = "Monthly Deduction" },
            new ColumnDefinition { ColumnLetter = "K", ExtractedHeaderName = "Fund Value After Monthly Deduction" },
            new ColumnDefinition { ColumnLetter = "L", ExtractedHeaderName = "Interest On Fund Value" },
            new ColumnDefinition { ColumnLetter = "M", ExtractedHeaderName = "Fund Value End of Month" },
            new ColumnDefinition { ColumnLetter = "N", ExtractedHeaderName = "Notes" }
        };

        var dataRows = new List<RawRowMetadata>();

        // Rows 6 to 53 (Era 1)
        for (int i = 6; i <= 53; i++)
        {
            var row = new RawRowMetadata
            {
                RowIndex = i,
                CellFormulas = new Dictionary<string, string>
                {
                    { "A", "" },
                    { "B", "" },
                    { "C", "+B" + i + "*Inputs!$B$12" }, // Col[B]*Inputs!$B$12
                    { "D", "+B" + i + "-C" + i },
                    { "E", "" },
                    { "F", "" },
                    { "G", "MAX(Inputs!$B$10, M" + (i-1) + "*Inputs!$B$21)" }, // Lookback!
                    { "H", "+G" + i + "-M" + (i-1) },
                    { "I", "0.00133*1000/12" }, // Era 1 mortality
                    { "J", "+E" + i + "+F" + i + "*(Inputs!$B$10/1000)+H" + i + "*(I" + i + "/1000)" },
                    { "K", "+M" + (i-1) + "+D" + i + "-J" + i },
                    { "L", "+K" + i + "*((1+Inputs!$B$3)^(1/12)-1)" },
                    { "M", "+K" + i + "+L" + i },
                    { "N", "" }
                },
                CellValues = new Dictionary<string, string>() // Mock values not needed for compression
            };
            dataRows.Add(row);
        }

        // Rows 54 to 65 (Era 2)
        for (int i = 54; i <= 65; i++)
        {
            var row = new RawRowMetadata
            {
                RowIndex = i,
                CellFormulas = new Dictionary<string, string>
                {
                    { "A", "" },
                    { "B", "" },
                    { "C", "+B" + i + "*Inputs!$B$12" },
                    { "D", "+B" + i + "-C" + i },
                    { "E", "" },
                    { "F", "" },
                    { "G", "MAX(Inputs!$B$10, M" + (i-1) + "*Inputs!$B$21)" },
                    { "H", "+G" + i + "-M" + (i-1) },
                    { "I", "0.00141*1000/12" }, // Era 2 mortality (this breaks the signature!)
                    { "J", "+E" + i + "+F" + i + "*(Inputs!$B$10/1000)+H" + i + "*(I" + i + "/1000)" },
                    { "K", "+M" + (i-1) + "+D" + i + "-J" + i },
                    { "L", "+K" + i + "*((1+Inputs!$B$3)^(1/12)-1)" },
                    { "M", "+K" + i + "+L" + i },
                    { "N", "" }
                },
                CellValues = new Dictionary<string, string>()
            };
            dataRows.Add(row);
        }

        return new RawWorkbookMap
        {
            SheetName = "Table 13.4",
            Headers = headers,
            DataRows = dataRows
        };
    }
}
