using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ActuarialTranslationEngine.Core.Persistence;

namespace ActuarialTranslationEngine.Persistence;

public class SqlitePersistenceManager : IPersistenceManager
{
    private readonly IDbContextFactory<ActuarialDbContext> _contextFactory;

    public SqlitePersistenceManager(IDbContextFactory<ActuarialDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        
        using var dbContext = _contextFactory.CreateDbContext();
    }

    public async Task<TranslationJobEntity> CreateJobAsync(Guid jobId, string originalFileName, string fileHash, string modelUsed, string targetSheet, Guid? workbookSessionId = null, CancellationToken cancellationToken = default)
    {
        var job = new TranslationJobEntity
        {
            Id = jobId,
            OriginalFileName = originalFileName,
            FileHash = fileHash,
            ModelUsed = modelUsed,
            TargetSheet = targetSheet,
            WorkbookSessionId = workbookSessionId,
            Status = TranslationJobStatus.Pending
        };

        await ExecuteWithRetryAsync(async dbContext =>
        {
            dbContext.TranslationJobs.Add(job);
            await dbContext.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return job;
    }

    public async Task UpdateJobStatusAsync(Guid jobId, TranslationJobStatus status, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async dbContext =>
        {
            var job = await dbContext.TranslationJobs.FindAsync(new object[] { jobId }, cancellationToken);
            if (job != null)
            {
                job.Status = status;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task SavePartitionAsync(TranslationPartitionEntity partition, CancellationToken cancellationToken = default)
    {
        if (partition == null) throw new ArgumentNullException(nameof(partition));

        await ExecuteWithRetryAsync(async dbContext =>
        {
            dbContext.TranslationPartitions.Add(partition);
            await dbContext.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<System.Collections.Generic.List<TranslationJobEntity>> GetPaginatedHistoryAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TranslationJobs
            .Include(j => j.Partitions)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<TranslationJobEntity?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TranslationJobs
            .Include(j => j.Partitions)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
    }

    public async Task<System.Collections.Generic.List<TranslationJobEntity>> GetJobsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TranslationJobs
            .Include(j => j.Partitions)
            .Where(j => j.WorkbookSessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task ExecuteWithRetryAsync(Func<ActuarialDbContext, Task> dbOperation, CancellationToken cancellationToken)
    {
        int maxRetries = 5;
        int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
                await dbOperation(dbContext);
                return; // Successfully saved
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is SqliteException sqliteEx && (sqliteEx.SqliteErrorCode == 5 || sqliteEx.SqliteErrorCode == 6))
            {
                // 5 is SQLITE_BUSY, 6 is SQLITE_LOCKED.
                if (i == maxRetries - 1)
                {
                    throw new InvalidOperationException($"Failed to execute db operation after {maxRetries} retries due to persistent SQLite database locks.", dbEx);
                }

                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
        }
    }
}
