using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Persistence;

public class TranslationPayload
{
    public List<TranslationOutput> Outputs { get; set; } = new();
}
