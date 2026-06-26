namespace ActuarialTranslationEngine.Core.Models;

public class TranslationJobRequest
{
    public Guid JobId { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = string.Empty;
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public string TargetSheet { get; set; } = "ALL";
    public string? ConnectionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public System.Threading.CancellationToken CancellationToken { get; set; } = default;
}
