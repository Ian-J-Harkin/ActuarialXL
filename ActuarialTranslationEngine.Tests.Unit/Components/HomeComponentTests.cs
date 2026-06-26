extern alias WebProject;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebProject::ActuarialTranslationEngine.Web.Components.Pages;
using WebProject::ActuarialTranslationEngine.Web.Services;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit.Components
{
    public class HomeComponentTests : BunitContext
    {
        public HomeComponentTests()
        {
            // Setup Configuration
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"ApiSettings:BaseUrl", "http://localhost"},
                    {"Testing:DisableSignalR", "true"}
                })
                .Build();
            Services.AddSingleton<IConfiguration>(config);
        }

        [Fact]
        public void Home_WhenAuditHistoryExists_ShowsChoiceScreen()
        {
            // Arrange
            var historyData = new List<HistorySummary>
            {
                new HistorySummary { Id = Guid.NewGuid(), OriginalFileName = "test.xlsx", Status = "Completed" }
            };

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == "/api/history")
                {
                    var json = JsonSerializer.Serialize(historyData);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ApiTranslationClient>();
            var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
            Services.AddSingleton(new ApiTranslationClient(httpClient, mockLogger));
            
            // Act
            var cut = Render<Home>();

            // Assert
            // Component should render the Choice Screen ("Welcome to Actuarial Governance")
            cut.WaitForState(() => cut.Markup.Contains("Welcome to Actuarial Governance"));
            Assert.Contains("Upload New Model", cut.Markup);
            Assert.Contains("View Audit History", cut.Markup);
        }

        [Fact]
        public void Home_WhenNoAuditHistoryExists_ShowsUploadScreen()
        {
            // Arrange
            var emptyHistory = new List<HistorySummary>(); // Empty ledger

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == "/api/history")
                {
                    var json = JsonSerializer.Serialize(emptyHistory);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ApiTranslationClient>();
            var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
            Services.AddSingleton(new ApiTranslationClient(httpClient, mockLogger));
            
            // Act
            var cut = Render<Home>();

            // Assert
            // Component should directly show the "Upload Model for Translation" screen
            cut.WaitForState(() => cut.Markup.Contains("Upload Model for Translation"));
            Assert.DoesNotContain("Welcome to Actuarial Governance", cut.Markup);
        }

        [Fact]
        public void Home_WhenDisruptiveNodesPresent_ShowsWarningBanner()
        {
            // Arrange
            var jobId = Guid.NewGuid();
            var jobDetails = new WebProject::ActuarialTranslationEngine.Web.Services.ApiTranslationResponse
            {
                TranslationId = jobId,
                Status = "Completed",
                Evaluations = new List<ActuarialTranslationEngine.Core.Models.TranslationOutput>
                {
                    new ActuarialTranslationEngine.Core.Models.TranslationOutput
                    {
                        IsCertified = true,
                        FinalAuditableMarkdown = "Test markdown",
                        GeneratedCSharpMirrorCode = "public class C {}",
                        DisruptiveNodes = new List<ActuarialTranslationEngine.Core.Models.DisruptiveNode>
                        {
                            new ActuarialTranslationEngine.Core.Models.DisruptiveNode 
                            { 
                                Coordinate = "Sheet1!B2", 
                                RawFormula = "=RAND()", 
                                ExceptionFlag = ActuarialTranslationEngine.Core.Exceptions.ActuarialNodeExceptionType.VolatileFunction 
                            }
                        }
                    }
                }
            };

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                if (request.RequestUri?.AbsolutePath == $"/api/history/{jobId}")
                {
                    var json = JsonSerializer.Serialize(jobDetails);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var mockLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ApiTranslationClient>();
            var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost") };
            Services.AddSingleton(new ApiTranslationClient(httpClient, mockLogger));
            
            // Act
            var cut = Render<Home>();
            
            cut.InvokeAsync(() => 
            {
                var responseField = typeof(Home).GetField("_response", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                responseField?.SetValue(cut.Instance, jobDetails);
                cut.Instance.GetType().GetMethod("StateHasChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(cut.Instance, null);
            });

            // Assert
            cut.WaitForState(() => cut.Markup.Contains("Spreadsheet Anomalies Detected"));
            Assert.Contains("Sheet1!B2", cut.Markup);
            Assert.Contains("=RAND()", cut.Markup);
            Assert.Contains("VolatileFunction", cut.Markup);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
