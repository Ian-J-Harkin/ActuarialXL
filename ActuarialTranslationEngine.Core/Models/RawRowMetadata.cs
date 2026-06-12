using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

public class RawRowMetadata
{
    public int RowIndex { get; init; }
    public Dictionary<string, string> CellFormulas { get; init; } = new();
    public Dictionary<string, string> CellValues { get; init; } = new();
    public List<DisruptiveNode> DisruptiveNodes { get; init; } = new();
}
