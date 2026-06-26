using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ActuarialTranslationEngine.API.Services;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using Microsoft.AspNetCore.SignalR;
using ActuarialTranslationEngine.API.Hubs;
using Microsoft.Extensions.Hosting;

namespace ActuarialTranslationEngine.Tests.Unit.Services
{
    public class BackgroundTranslationWorkerTests
    {
        [Fact]
        public async Task ProcessJobAsync_PassesJobIdToPersistenceManager()
        {
            // Arrange
            var expectedJobId = Guid.NewGuid();
            var mockPersistence = new MockPersistenceManager();
            var mockQueue = new MockTranslationJobQueue(new TranslationJobRequest
            {
                JobId = expectedJobId,
                OriginalFileName = "test.xlsx",
                FileData = new byte[] { 0x50, 0x4B }
            });

            var services = new ServiceCollection();
            services.AddSingleton<IActuarialExtractionEngine>(new DummyExtractionEngine());
            services.AddSingleton<IVectorCompressionEngine>(new DummyCompressionEngine());
            services.AddSingleton<IReconciliationOrchestrator>(new DummyOrchestrator());
            services.AddSingleton<IPersistenceManager>(mockPersistence);
            services.AddSingleton<IHubContext<TranslationProgressHub>>(new DummyHubContext());

            var serviceProvider = services.BuildServiceProvider();

            // BackgroundTranslationWorker is an IHostedService
            var worker = new BackgroundTranslationWorker(mockQueue, serviceProvider, new NullLogger<BackgroundTranslationWorker>());

            // Act
            using var cts = new CancellationTokenSource();
            
            // ExecuteAsync is protected, but we can call StartAsync which triggers it.
            await worker.StartAsync(cts.Token);
            
            // Wait briefly to allow the background task to dequeue and process
            await Task.Delay(100);
            
            cts.Cancel();
            await worker.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(mockPersistence.CreateJobCalled, "CreateJobAsync was never called on the PersistenceManager.");
            Assert.Equal(expectedJobId, mockPersistence.PassedJobId);
        }

        private class MockTranslationJobQueue : ITranslationJobQueue
        {
            private readonly TranslationJobRequest _request;
            private bool _returned = false;

            public MockTranslationJobQueue(TranslationJobRequest request)
            {
                _request = request;
            }

            public ValueTask EnqueueJobAsync(TranslationJobRequest request, CancellationToken cancellationToken = default)
            {
                return ValueTask.CompletedTask;
            }

            public async ValueTask<TranslationJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default)
            {
                if (!_returned)
                {
                    _returned = true;
                    return _request;
                }
                // Block forever on subsequent calls until cancelled
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new OperationCanceledException();
            }
        }

        private class MockPersistenceManager : IPersistenceManager
        {
            public bool CreateJobCalled { get; private set; }
            public Guid PassedJobId { get; private set; }

            public Task<TranslationJobEntity> CreateJobAsync(Guid jobId, string originalFileName, string fileHash, string modelUsed, string targetSheet = "ALL", Guid? workbookSessionId = null, CancellationToken cancellationToken = default)
            {
                CreateJobCalled = true;
                PassedJobId = jobId;
                return Task.FromResult(new TranslationJobEntity { Id = jobId });
            }

            public Task UpdateJobStatusAsync(Guid jobId, TranslationJobStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task SavePartitionAsync(TranslationPartitionEntity partition, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<List<TranslationJobEntity>> GetPaginatedHistoryAsync(int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<TranslationJobEntity>());
            public Task<TranslationJobEntity?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.FromResult<TranslationJobEntity?>(null);
            public Task<List<TranslationJobEntity>> GetJobsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(new List<TranslationJobEntity>());
        }

        private class DummyExtractionEngine : IActuarialExtractionEngine
        {
            public List<string> GetWorksheetNames(Stream workbookStream) => throw new NotImplementedException();
            public RawWorkbookMap ExtractSheetData(Stream workbookStream, string sheetName) => throw new NotImplementedException();
        }

        private class DummyCompressionEngine : IVectorCompressionEngine
        {
            public CompressedVectorBlock CompressTopology(RawWorkbookMap map) => throw new NotImplementedException();
        }

        private class DummyOrchestrator : IReconciliationOrchestrator
        {
            public IAsyncEnumerable<TranslationOutput> ProcessBlockAsync(CompressedVectorBlock block, RawWorkbookMap originalMap, IProgress<TranslationProgressEvent>? progress = null, CancellationToken cancellationToken = default(CancellationToken)) => throw new NotImplementedException();
            public Task<List<TranslationOutput>> ProcessVbaModulesAsync(List<VbaModuleCode> vbaModules, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class DummyHubContext : IHubContext<TranslationProgressHub>
        {
            public IHubClients Clients => new DummyHubClients();
            public IGroupManager Groups => throw new NotImplementedException();
        }

        private class DummyHubClients : IHubClients
        {
            public IClientProxy All => throw new NotImplementedException();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
            public IClientProxy Client(string connectionId) => throw new NotImplementedException();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
            public IClientProxy Group(string groupName) => new DummyClientProxy();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
            public IClientProxy User(string userId) => throw new NotImplementedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
        }

        private class DummyClientProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
