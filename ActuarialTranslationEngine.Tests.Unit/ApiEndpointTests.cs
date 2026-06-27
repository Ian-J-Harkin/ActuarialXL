using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.API.Services;
using ActuarialTranslationEngine.API.Endpoints;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActuarialTranslationEngine.Tests.Unit
{
    public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ApiEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Post_SessionUpload_WithNoFile_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/api/session/upload", new MultipartFormDataContent());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Post_SessionUpload_WithInvalidFileType_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x00 });
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileContent, "file", "test.txt");

            var response = await client.PostAsync("/api/session/upload", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("Unsupported file type", responseString);
        }

        [Fact]
        public async Task Post_SessionUpload_WithLargeFile_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[6 * 1024 * 1024]); // 6MB
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", "large.xlsx");

            var response = await client.PostAsync("/api/session/upload", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("exceeds the 5MB upload limit", responseString);
        }

        [Fact]
        public async Task Post_SessionUpload_WithInvalidMagicBytes_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var content = new MultipartFormDataContent();
            // Valid extension, but invalid magic bytes (not 0x50, 0x4B)
            var fileContent = new ByteArrayContent(new byte[] { 0x00, 0x00, 0x00 });
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", "fake.xlsx");

            var response = await client.PostAsync("/api/session/upload", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid file signature", responseString);
        }

        [Fact]
        public async Task Post_SessionUpload_WithValidFile_ReturnsOk()
        {
            var mockQueue = new MockTranslationJobQueue();

            var clientWithMocks = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<ITranslationJobQueue>();
                    services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
                    services.RemoveAll<IActuarialExtractionEngine>();
                    
                    services.AddSingleton<ITranslationJobQueue>(mockQueue);
                    services.AddSingleton<IActuarialExtractionEngine>(new DummyExtractionEngineForTests());
                });
            }).CreateClient();

            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", "dummy.xlsx");

            var response = await clientWithMocks.PostAsync("/api/session/upload", content);

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("sessionId", responseString, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Post_Execute_WithValidJob_EnqueuesToWorker()
        {
            var mockQueue = new MockTranslationJobQueue();
            var mockPersistence = new MockPersistenceManager();

            var clientWithMocks = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<ITranslationJobQueue>();
                    services.RemoveAll<IPersistenceManager>();
                    services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
                    
                    services.AddSingleton<ITranslationJobQueue>(mockQueue);
                    services.AddSingleton<IPersistenceManager>(mockPersistence);
                });
            }).CreateClient();

            // Create a dummy file in the uploads directory so it exists
            var sessionId = Guid.NewGuid();
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            Directory.CreateDirectory(uploadDir);
            File.WriteAllText(Path.Combine(uploadDir, $"{sessionId}.xlsx"), "dummy content");

            var jobId = Guid.NewGuid();
            var response = await clientWithMocks.PostAsync($"/api/session/{sessionId}/execute/{jobId}", new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal(1, mockQueue.EnqueueCount);
        }

        [Fact]
        public async Task Post_Execute_WithAlreadyRunningJob_ReturnsConflict()
        {
            var mockQueue = new MockTranslationJobQueue();
            var mockPersistence = new MockPersistenceManager();

            var clientWithMocks = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<ITranslationJobQueue>();
                    services.RemoveAll<IPersistenceManager>();
                    services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
                    
                    services.AddSingleton<ITranslationJobQueue>(mockQueue);
                    services.AddSingleton<IPersistenceManager>(mockPersistence);
                });
            }).CreateClient();

            var sessionId = Guid.NewGuid();
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            Directory.CreateDirectory(uploadDir);
            File.WriteAllText(Path.Combine(uploadDir, $"{sessionId}.xlsx"), "dummy content");

            // Magic GUID that MockPersistenceManager treats as Running
            var jobId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var response = await clientWithMocks.PostAsync($"/api/session/{sessionId}/execute/{jobId}", new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(0, mockQueue.EnqueueCount);
        }
        
        [Fact]
        public async Task Post_Execute_WithDeletedFile_ReturnsNotFound()
        {
            var client = _factory.CreateClient();
            var sessionId = Guid.NewGuid(); // Random GUID so the file definitely doesn't exist
            var jobId = Guid.NewGuid();
            var response = await client.PostAsync($"/api/session/{sessionId}/execute/{jobId}", new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    public class MockTranslationJobQueue : ITranslationJobQueue
    {
        public int EnqueueCount { get; private set; }

        public ValueTask EnqueueJobAsync(TranslationJobRequest request, CancellationToken cancellationToken = default)
        {
            EnqueueCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<TranslationJobRequest> DequeueJobAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<TranslationJobRequest>(new TranslationJobRequest());
        }
    }

    public class DummyExtractionEngineForTests : IActuarialExtractionEngine
    {
        public List<string> GetWorksheetNames(Stream workbookStream) => new List<string> { "Sheet1" };
        public RawWorkbookMap ExtractSheetData(Stream workbookStream, string sheetName) => throw new NotImplementedException();
    }

    public class MockPersistenceManager : IPersistenceManager
    {
        public Task<TranslationJobEntity> CreateJobAsync(Guid jobId, string originalFileName, string fileHash, string modelUsed, string targetSheet, Guid? workbookSessionId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TranslationJobEntity { Id = jobId, OriginalFileName = originalFileName, TargetSheet = targetSheet, Status = TranslationJobStatus.Pending });
        }
        public Task UpdateJobStatusAsync(Guid jobId, TranslationJobStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateJobTargetSheetAsync(Guid jobId, string targetSheet, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SavePartitionAsync(TranslationPartitionEntity partition, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<TranslationJobEntity>> GetPaginatedHistoryAsync(int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult(new List<TranslationJobEntity>());
        public Task<TranslationJobEntity?> GetJobDetailsAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            if (jobId == Guid.Empty) return Task.FromResult<TranslationJobEntity?>(null);
            
            // Magic GUID to simulate running
            if (jobId == Guid.Parse("11111111-1111-1111-1111-111111111111"))
            {
                return Task.FromResult<TranslationJobEntity?>(new TranslationJobEntity { Id = jobId, Status = TranslationJobStatus.Running, OriginalFileName = "test.xlsx" });
            }
            
            return Task.FromResult<TranslationJobEntity?>(new TranslationJobEntity { Id = jobId, Status = TranslationJobStatus.Pending, OriginalFileName = "test.xlsx", TargetSheet = "Sheet1" });
        }
        public Task<List<TranslationJobEntity>> GetJobsBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TranslationJobEntity> { new TranslationJobEntity { Id = Guid.NewGuid(), Status = TranslationJobStatus.Pending } });
        }
    }
}
