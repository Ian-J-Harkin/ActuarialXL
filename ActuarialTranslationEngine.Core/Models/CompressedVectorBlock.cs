using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

public class CompressedVectorBlock
{
    public required string TargetWorksheet { get; init; }
    public string ProcessingArchetype { get; set; } = string.Empty;
    public List<VectorRangePartition> Partitions { get; init; } = new();
    public List<DisruptiveNode> DisruptiveNodes { get; init; } = new();
}
