using System;

namespace ActuarialTranslationEngine.Core.Persistence;

public class TranslationPartitionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid JobId { get; set; }
    public TranslationJobEntity Job { get; set; } = null!;
    
    public int PartitionIndex { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string FinalAuditableMarkdown { get; set; } = string.Empty;
    public string GeneratedCSharpMirrorCode { get; set; } = string.Empty;
    public bool IsCertified { get; set; } = true;
    public decimal? VarianceDelta { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DisruptiveNodesJson { get; set; }
}
