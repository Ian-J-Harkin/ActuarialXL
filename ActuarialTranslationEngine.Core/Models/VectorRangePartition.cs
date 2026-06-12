using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

public class VectorRangePartition
{
    public int StartRow { get; set; }
    public int EndRow { get; set; }
    public required string FormulaSignature { get; init; }
    public List<ColumnDefinition> StructuralColumns { get; init; } = new();
}
