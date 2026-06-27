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

public class TestDbContextFactory : IDbContextFactory<ActuarialDbContext>
{
    private readonly DbContextOptions<ActuarialDbContext> _options;
    public TestDbContextFactory(DbContextOptions<ActuarialDbContext> options) => _options = options;
    public ActuarialDbContext CreateDbContext() => new ActuarialDbContext(_options);
}

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
    public async Task CreateJobAsync_ShouldStoreJob_Successfully()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var manager = new SqlitePersistenceManager(factory);

        // Act
        var job = await manager.CreateJobAsync(Guid.NewGuid(), "test.xlsx", "hash123", "model-abc", "ALL", null);

        // Assert
        using var verifyContext = new ActuarialDbContext(_options);
        var storedJob = await verifyContext.TranslationJobs.FindAsync(job.Id);

        Assert.NotNull(storedJob);
        Assert.Equal("test.xlsx", storedJob.OriginalFileName);
        Assert.Equal("hash123", storedJob.FileHash);
        Assert.Equal("model-abc", storedJob.ModelUsed);
        Assert.Equal(TranslationJobStatus.Pending, storedJob.Status);
    }

    [Fact]
    public async Task SavePartitionAsync_ShouldStorePartition_Successfully()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var manager = new SqlitePersistenceManager(factory);
        var job = await manager.CreateJobAsync(Guid.NewGuid(), "test.xlsx", "hash", "model", "ALL", null);

        var partition = new TranslationPartitionEntity
        {
            JobId = job.Id,
            PartitionIndex = 1,
            FinalAuditableMarkdown = "# Hello",
            GeneratedCSharpMirrorCode = "public class C {}"
        };

        // Act
        await manager.SavePartitionAsync(partition);

        // Assert
        using var verifyContext = new ActuarialDbContext(_options);
        var storedPartition = await verifyContext.TranslationPartitions.FirstOrDefaultAsync(p => p.Id == partition.Id);

        Assert.NotNull(storedPartition);
        Assert.Equal(job.Id, storedPartition.JobId);
        Assert.Equal(1, storedPartition.PartitionIndex);
        Assert.Equal("# Hello", storedPartition.FinalAuditableMarkdown);
    }

    [Fact]
    public async Task UpdateJobStatusAsync_ShouldChangeStatus()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var manager = new SqlitePersistenceManager(factory);
        var job = await manager.CreateJobAsync(Guid.NewGuid(), "test.xlsx", "hash", "model", "ALL", null);

        // Act
        await manager.UpdateJobStatusAsync(job.Id, TranslationJobStatus.Completed);

        // Assert
        using var verifyContext = new ActuarialDbContext(_options);
        var storedJob = await verifyContext.TranslationJobs.FindAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(TranslationJobStatus.Completed, storedJob.Status);
    }

    [Fact]
    public async Task SavePartitionAsync_ShouldRetryAndThrow_WhenDatabaseIsLocked()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var manager = new SqlitePersistenceManager(factory);
        var job = await manager.CreateJobAsync(Guid.NewGuid(), "test.xlsx", "hash", "model", "ALL", null);

        var partition = new TranslationPartitionEntity
        {
            JobId = job.Id,
            PartitionIndex = 1
        };

        // Create a secondary connection and execute a write to lock the database exclusively
        using var secondaryConnection = new SqliteConnection(_connectionString);
        secondaryConnection.Open();
        
        using var transaction = secondaryConnection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        
        using var command = secondaryConnection.CreateCommand();
        command.CommandText = "INSERT INTO TranslationPartitions (Id, JobId, PartitionIndex, FinalAuditableMarkdown, GeneratedCSharpMirrorCode, SourceName, IsCertified) VALUES (@id, @j, @i, @f, @g, @s, 1)";
        command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@j", job.Id.ToString().ToUpperInvariant());
        command.Parameters.AddWithValue("@i", 2);
        command.Parameters.AddWithValue("@f", "");
        command.Parameters.AddWithValue("@g", "");
        command.Parameters.AddWithValue("@s", "");
        command.ExecuteNonQuery();

        // Act & Assert
        // Since the DB is locked by the transaction on the secondary connection, this will retry and eventually throw.
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SavePartitionAsync(partition));
    }

    [Fact]
    public async Task GetPaginatedHistoryAsync_ShouldSortDeterministically_WhenTimestampsAreIdentical()
    {
        // Arrange
        var factory = new TestDbContextFactory(_options);
        var manager = new SqlitePersistenceManager(factory);
        var time = DateTime.UtcNow;

        // Create two jobs with identical timestamps manually
        using (var dbContext = factory.CreateDbContext())
        {
            var job1 = new TranslationJobEntity { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), OriginalFileName = "test.xlsx", FileHash = "1", ModelUsed = "M", TargetSheet = "Sheet1", Status = TranslationJobStatus.Pending, CreatedAt = time };
            var job2 = new TranslationJobEntity { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), OriginalFileName = "test.xlsx", FileHash = "2", ModelUsed = "M", TargetSheet = "Sheet2", Status = TranslationJobStatus.Pending, CreatedAt = time };
            
            dbContext.TranslationJobs.AddRange(job1, job2);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var results = await manager.GetPaginatedHistoryAsync(0, 10);

        // Assert
        Assert.Equal(2, results.Count);
        // The one with the smaller GUID (111...) should come first due to ThenBy(x => x.Id)
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), results[0].Id);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), results[1].Id);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
