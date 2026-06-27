using System;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Models;

namespace ActuarialTranslationEngine.Core.Persistence;

public interface IPersistenceManager
{
    Task<TranslationJobEntity> CreateJobAsync(Guid jobId, string originalFileName, string fileHash, string modelUsed, string targetSheet, Guid? workbookSessionId = null, CancellationToken cancellationToken = default);
    Task UpdateJobStatusAsync(Guid jobId, TranslationJobStatus status, CancellationToken cancellationToken = default);
    Task UpdateJobTargetSheetAsync(Guid jobId, string targetSheet, CancellationToken cancellationToken = default);
    Task SavePartitionAsync(TranslationPartitionEntity partition, CancellationToken cancellationToken = default);
    Task<System.Collections.Generic.List<TranslationJobEntity>> GetPaginatedHistoryAsync(int skip, int take, CancellationToken cancellationToken = default);
    Task<TranslationJobEntity?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<System.Collections.Generic.List<TranslationJobEntity>> GetJobsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
