using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.Persistence;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit.Persistence;

public class SqlitePersistenceManagerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ActuarialDbContext> _options;
    private readonly string _connectionString;

    public SqlitePersistenceManagerTests()
    {
        // Use a shared in-memory database so multiple connections can interact with the same DB.
        _connectionString = $"DataSource=file:memdb_{Guid.NewGuid()}?mode=memory&cache=shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        _options = new DbContextOptionsBuilder<ActuarialDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ActuarialDbContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveTranslationAsync_ShouldStoreJsonbPayload_Successfully()
    {
        // Arrange
        using var context = new ActuarialDbContext(_options);
        var manager = new SqlitePersistenceManager(context);

        var recordId = Guid.NewGuid();
        var record = new TranslatedModelRecord
        {
            Id = recordId,
            OriginalFileName = "test.xlsx",
            FileHash = "hash123",
            Payload = new TranslationPayload
            {
                Output = new TranslationOutput
                {
                    FinalAuditableMarkdown = "# Hello",
                    GeneratedCSharpMirrorCode = "public class C {}"
                }
            }
        };

        // Act
        await manager.SaveTranslationAsync(record);

        // Assert
        using var verifyContext = new ActuarialDbContext(_options);
        var storedRecord = await verifyContext.TranslatedModels.FindAsync(recordId);

        Assert.NotNull(storedRecord);
        Assert.Equal("test.xlsx", storedRecord.OriginalFileName);
        Assert.NotNull(storedRecord.Payload);
        Assert.NotNull(storedRecord.Payload.Output);
        Assert.Equal("# Hello", storedRecord.Payload.Output.FinalAuditableMarkdown);
    }

    [Fact]
    public async Task SaveTranslationAsync_ShouldRetryAndThrow_WhenDatabaseIsLocked()
    {
        // Arrange
        using var context = new ActuarialDbContext(_options);
        var manager = new SqlitePersistenceManager(context);

        var record = new TranslatedModelRecord
        {
            Id = Guid.NewGuid(),
            Payload = new TranslationPayload()
        };

        // Create a secondary connection and execute a write to lock the database exclusively
        using var secondaryConnection = new SqliteConnection(_connectionString);
        secondaryConnection.Open();
        
        using var transaction = secondaryConnection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        
        using var command = secondaryConnection.CreateCommand();
        command.CommandText = "INSERT INTO TranslatedModels (Id, CreatedAt, FileHash, OriginalFileName, Payload) VALUES (@id, @c, @f, @o, @p)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("@f", "");
        command.Parameters.AddWithValue("@o", "");
        command.Parameters.AddWithValue("@p", "{}");
        command.ExecuteNonQuery();

        // Act & Assert
        // The DB is now locked by the uncommitted write transaction, causing a SQLITE_BUSY error for manager.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SaveTranslationAsync(record));
        Assert.Contains("Failed to save translation after 5 retries", ex.Message);
        
        transaction.Rollback();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
