namespace ActuarialTranslationEngine.Core.Models;

public class TranslationOutput
{
    public required string FinalAuditableMarkdown { get; init; }
    public required string GeneratedCSharpMirrorCode { get; init; }
}
