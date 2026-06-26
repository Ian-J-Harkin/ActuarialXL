using System;
using System.Collections.Generic;

namespace ActuarialTranslationEngine.Core.Persistence;

public enum TranslationJobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Canceled
}

public class TranslationJobEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public Guid? WorkbookSessionId { get; set; }
    public string TargetSheet { get; set; } = "ALL";
    public TranslationJobStatus Status { get; set; } = TranslationJobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TranslationPartitionEntity> Partitions { get; set; } = new List<TranslationPartitionEntity>();
}
