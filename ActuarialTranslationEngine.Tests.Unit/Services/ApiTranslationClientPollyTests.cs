extern alias WebProject;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebProject::ActuarialTranslationEngine.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Xunit;

namespace ActuarialTranslationEngine.Tests.Unit.Services
{
    public class ApiTranslationClientPollyTests
    {
        [Fact]
        public async Task ApiTranslationClient_ShouldRetry_OnTransientHttpErrors()
        {
            // Arrange
            var retryCount = 0;
            var mockHandler = new MockHttpMessageHandler(() =>
            {
                retryCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); // 503 is a transient error
            });

            var services = new ServiceCollection();
            
            services.AddHttpClient<ApiTranslationClient>(client => 
            {
                client.BaseAddress = new Uri("http://localhost:5158");
            })
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler)
            .AddTransientHttpErrorPolicy(policyBuilder =>
                policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(1))); // Fast retry for tests

            var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<ApiTranslationClient>();

            // Act
            var result = await client.GetHistoricalTranslationAsync(Guid.NewGuid());
            
            // Assert
            Assert.Null(result);
            Assert.Equal(4, retryCount); // 1 initial + 3 retries
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _responseFunc;

            public MockHttpMessageHandler(Func<HttpResponseMessage> responseFunc)
            {
                _responseFunc = responseFunc;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responseFunc());
            }
        }
    }
}
