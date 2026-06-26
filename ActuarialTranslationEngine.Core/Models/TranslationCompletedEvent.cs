namespace ActuarialTranslationEngine.Core.Models;

public class TranslationCompletedEvent
{
    public Guid TranslationId { get; set; }
    public string Message { get; set; } = "Translation complete.";
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
