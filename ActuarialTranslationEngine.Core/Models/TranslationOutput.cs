namespace ActuarialTranslationEngine.Core.Models;

public class TranslationOutput
{
    public string SourceName { get; init; } = string.Empty;
    public required string FinalAuditableMarkdown { get; init; }
    public required string GeneratedCSharpMirrorCode { get; init; }
    public bool IsCertified { get; init; }
    public decimal? VarianceDelta { get; init; }
    public string? ErrorMessage { get; init; }
    public List<DisruptiveNode> DisruptiveNodes { get; init; } = new();
}
