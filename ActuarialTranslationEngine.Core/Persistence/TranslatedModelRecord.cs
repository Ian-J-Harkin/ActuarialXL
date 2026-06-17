using System;
using System.Collections.Generic;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Persistence;

public class TranslatedModelRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The JSONB payload mapped by EF Core
    public TranslationPayload Payload { get; set; } = new TranslationPayload();
}
