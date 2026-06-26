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

/// <summary>
/// Tests for LiveDomainInterrogationBridge.ParseLlmOutput, covering all four parsing paths
/// plus the HTTP-level failure modes.
///
/// Parsing contract (four paths):
///   Path 1 — Delimiter + ```csharp``` fence:       split on delimiter, strip fence
///   Path 2 — Delimiter + raw C#:                   split on delimiter, use code as-is
///   Path 3 — No delimiter + recognisable class:    Postel's Law graceful extraction
///   Path 4 — No delimiter + no class:              unrecoverable → throw
/// </summary>
public class LiveDomainInterrogationBridgeTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LiveDomainInterrogationBridge CreateSut(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var config = new LlmBridgeConfiguration();
        var handler = new MockHttpMessageHandler(responseContent, statusCode);
        var client = new HttpClient(handler);
        return new LiveDomainInterrogationBridge(client, config);
    }

    private static string WrapInChoices(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        });

    // ── Path 1: Delimiter + ```csharp``` fence ───────────────────────────────

    [Fact]
    public async Task ProcessPayloadAsync_Path1_ParsesCorrectly_WithDelimiterAndMarkdownFence()
    {
        // Arrange
        var llmContent = "This is the markdown.\n===CSHARP_MIRROR===\n```csharp\npublic class X { }\n```";
        var sut = CreateSut(WrapInChoices(llmContent));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act
        var result = await sut.ProcessPayloadAsync(payload, "G");

        // Assert
        Assert.Equal("This is the markdown.", result.FinalAuditableMarkdown);
        Assert.Contains("public class X { }", result.GeneratedCSharpMirrorCode);
        Assert.Contains("using ActuarialTranslationEngine.Core.Interfaces;", result.GeneratedCSharpMirrorCode);
    }

    // ── Path 2: Delimiter + raw C# (no fence) ───────────────────────────────

    [Fact]
    public async Task ProcessPayloadAsync_Path2_ParsesCorrectly_WithDelimiterAndRawCode()
    {
        // Arrange
        var llmContent = "Analysis complete.\n===CSHARP_MIRROR===\npublic class RawClass { public decimal ExecuteCalculationRow(System.Collections.Generic.Dictionary<string, decimal> inputs) => 0m; }";
        var sut = CreateSut(WrapInChoices(llmContent));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act
        var result = await sut.ProcessPayloadAsync(payload, "G");

        // Assert
        Assert.Equal("Analysis complete.", result.FinalAuditableMarkdown);
        Assert.Contains("public class RawClass", result.GeneratedCSharpMirrorCode);
        // Markdown section should be empty — code was bare, no fence stripping needed
        Assert.DoesNotContain("===CSHARP_MIRROR===", result.GeneratedCSharpMirrorCode);
    }

    // ── Path 3: No delimiter, but class present (Postel's Law) ─────────────

    [Fact]
    public async Task ProcessPayloadAsync_Path3_ExtractsGracefully_WhenNoDelimiterButClassPresent()
    {
        // Arrange — LLM forgot the delimiter but included a valid class with conversational preamble
        var llmContent = "Here is the translation you asked for:\n\npublic class FundValueModel { public decimal ExecuteCalculationRow(System.Collections.Generic.Dictionary<string, decimal> inputs) => inputs[\"A\"] * 2m; }";
        var sut = CreateSut(WrapInChoices(llmContent));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act
        var result = await sut.ProcessPayloadAsync(payload, "G");

        // Assert — should return a result, not throw
        Assert.Contains("public class FundValueModel", result.GeneratedCSharpMirrorCode);
        // Markdown will be empty — no delimiter to split on
        Assert.Equal("", result.FinalAuditableMarkdown);
    }

    [Fact]
    public async Task ProcessPayloadAsync_Path3_ExtractsGracefully_WhenNoDelimiterButClassInFence()
    {
        // Arrange — LLM forgot delimiter, wrapped code in a fence, included preamble
        var llmContent = "Sure, here it is:\n```csharp\npublic class FencedModel { }\n```";
        var sut = CreateSut(WrapInChoices(llmContent));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act
        var result = await sut.ProcessPayloadAsync(payload, "G");

        // Assert
        Assert.Contains("public class FencedModel", result.GeneratedCSharpMirrorCode);
    }

    // ── Path 4: No delimiter AND no recognisable class ──────────────────────

    [Fact]
    public async Task ProcessPayloadAsync_Path4_Throws_WhenNoDelimiterAndNoClassDeclaration()
    {
        // Arrange — pure conversational response, no C# at all
        var llmContent = "I forgot the delimiter.";
        var sut = CreateSut(WrapInChoices(llmContent));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ActuarialLlmBridgeException>(() => sut.ProcessPayloadAsync(payload, "G"));
        Assert.Contains("delimiter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessPayloadAsync_Path4_Throws_WhenResponseIsCompletelyEmpty()
    {
        // Arrange — LLM returns empty string in content
        var sut = CreateSut(WrapInChoices(""));
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act & Assert — null/empty content should throw
        await Assert.ThrowsAsync<ActuarialLlmBridgeException>(() => sut.ProcessPayloadAsync(payload, "G"));
    }

    // ── HTTP-level failures ──────────────────────────────────────────────────

    [Fact]
    public async Task ProcessPayloadAsync_Throws_OnNonSuccessHttpStatusCode()
    {
        // Arrange — API returns 401 Unauthorized
        var sut = CreateSut("{\"error\":\"Unauthorized\"}", HttpStatusCode.Unauthorized);
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act & Assert — bridge must throw, and the message must name the failing status
        var ex = await Assert.ThrowsAsync<ActuarialLlmBridgeException>(() => sut.ProcessPayloadAsync(payload, "G"));
        Assert.Contains("Unauthorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessPayloadAsync_Throws_OnMalformedJsonResponse()
    {
        // Arrange — API returns garbage instead of JSON
        var sut = CreateSut("this is not json at all }{", HttpStatusCode.OK);
        var payload = new CompressedVectorBlock { TargetWorksheet = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<ActuarialLlmBridgeException>(() => sut.ProcessPayloadAsync(payload, "G"));
    }
}

// ── Test infrastructure ──────────────────────────────────────────────────────

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
