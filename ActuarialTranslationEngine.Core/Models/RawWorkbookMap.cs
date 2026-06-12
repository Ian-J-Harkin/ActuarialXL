using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Models;

public class RawWorkbookMap
{
    public required string SheetName { get; init; }
    public List<ColumnDefinition> Headers { get; init; } = new();
    public List<RawRowMetadata> DataRows { get; init; } = new();
}
