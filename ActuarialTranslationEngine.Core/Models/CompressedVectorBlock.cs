using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

public class CompressedVectorBlock
{
    public required string TargetWorksheet { get; init; }
    public List<VectorRangePartition> Partitions { get; init; } = new();
}
