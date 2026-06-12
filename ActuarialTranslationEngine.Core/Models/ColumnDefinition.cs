namespace ActuarialTranslationEngine.Core.Models;

public class ColumnDefinition
{
    public required string ColumnLetter { get; init; }
    public required string ExtractedHeaderName { get; init; }
    public string TokenizedFormulaTemplate { get; set; } = string.Empty;
    public List<int> ChronologicalLookbacks { get; set; } = new();
}
