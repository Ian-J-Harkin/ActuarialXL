using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.API.Services;
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
        public async Task Post_Evaluate_WithNoFile_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/api/evaluate", new MultipartFormDataContent());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Post_Evaluate_WithInvalidFileType_ReturnsBadRequest()
        {
            var client = _factory.CreateClient();
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x00 });
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            content.Add(fileContent, "file", "test.txt");

            var response = await client.PostAsync("/api/evaluate", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("Unsupported file type", responseString);
        }

        [Fact]
        public async Task Post_Evaluate_WithValidFile_ReturnsAccepted()
        {
            var mockQueue = new MockTranslationJobQueue();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing job queue if any
                    services.RemoveAll<ITranslationJobQueue>();
                    services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
                    
                    // Add mock
                    services.AddSingleton<ITranslationJobQueue>(mockQueue);
                });
            }).CreateClient();

            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(new byte[] { 0x50, 0x4B });
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", "dummy.xlsx");

            var response = await client.PostAsync("/api/evaluate", content);

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Contains("jobId", responseString, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Accepted", responseString, StringComparison.OrdinalIgnoreCase);
            
            // Assert that the job was enqueued
            Assert.Equal(1, mockQueue.EnqueueCount);
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
}
