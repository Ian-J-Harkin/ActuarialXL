namespace ActuarialTranslationEngine.Tests.Unit.LlmBridge;

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine.LlmBridge;
using Xunit;

public class LiveDomainInterrogationBridgeTests
{
    [Fact]
    public async Task ProcessPayloadAsync_ParsesCorrectResponse_WithMarkdownBlock()
    {
        // Arrange
        var config = new LlmBridgeConfiguration();
        
        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new 
                {
                    message = new 
                    {
                        content = "This is the markdown.\n===CSHARP_MIRROR===\n```csharp\npublic class X { }\n```"
                    }
                }
            }
        });

        var handler = new MockHttpMessageHandler(jsonResponse, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var sut = new LiveDomainInterrogationBridge(client, config);

        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act
        var result = await sut.ProcessPayloadAsync(payload);

        // Assert
        Assert.Equal("This is the markdown.", result.FinalAuditableMarkdown);
        Assert.Contains("public class X { }", result.GeneratedCSharpMirrorCode);
        Assert.Contains("using ActuarialTranslationEngine.Core.Interfaces;", result.GeneratedCSharpMirrorCode);
    }

    [Fact]
    public async Task ProcessPayloadAsync_ThrowsException_OnMissingDelimiter()
    {
        var config = new LlmBridgeConfiguration();
        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = "I forgot the delimiter." } } }
        });

        var handler = new MockHttpMessageHandler(jsonResponse, HttpStatusCode.OK);
        var client = new HttpClient(handler);
        var sut = new LiveDomainInterrogationBridge(client, config);

        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        await Assert.ThrowsAsync<ActuarialLlmBridgeException>(() => sut.ProcessPayloadAsync(payload));
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseContent;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent)
        };
        return Task.FromResult(response);
    }
}
