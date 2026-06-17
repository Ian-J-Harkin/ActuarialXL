using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ActuarialTranslationEngine.Core.Persistence;

namespace ActuarialTranslationEngine.Persistence;

public class SqlitePersistenceManager : IPersistenceManager
{
    private readonly ActuarialDbContext _dbContext;

    public SqlitePersistenceManager(ActuarialDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbContext.Database.EnsureCreated(); // Creates schema on first run; no-op thereafter.
    }

    public async Task SaveTranslationAsync(TranslatedModelRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));

        // Implementing exponential backoff retry logic for SQLite's SQLITE_BUSY/SQLITE_LOCKED.
        int maxRetries = 5;
        int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Add inside the loop so that on retry the entity is re-tracked cleanly.
                _dbContext.TranslatedModels.Add(record);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return; // Successfully saved
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is SqliteException sqliteEx && (sqliteEx.SqliteErrorCode == 5 || sqliteEx.SqliteErrorCode == 6))
            {
                // 5 is SQLITE_BUSY, 6 is SQLITE_LOCKED.
                // Detach the entity so the next retry can re-add it with a clean state.
                _dbContext.Entry(record).State = EntityState.Detached;

                if (i == maxRetries - 1)
                {
                    throw new InvalidOperationException($"Failed to save translation after {maxRetries} retries due to persistent SQLite database locks.", dbEx);
                }

                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
        }
    }
}
