namespace ActuarialTranslationEngine.Engine.LlmBridge;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ActuarialTranslationEngine.Core.Exceptions;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;

public class LiveDomainInterrogationBridge : IDomainInterrogationBridge
{
    private readonly HttpClient _httpClient;
    private readonly LlmBridgeConfiguration _config;

    public LiveDomainInterrogationBridge(HttpClient httpClient, LlmBridgeConfiguration config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        
        var userContent = $"Payload: \n{payloadJson}";
        
        if (!string.IsNullOrEmpty(previousCompilerError))
        {
            userContent += $"\n\nYOUR PREVIOUS ATTEMPT FAILED WITH COMPILER ERRORS:\n{previousCompilerError}\n\nPlease fix the errors and try again. Output the corrected class.";
        }

        var requestBody = new
        {
            model = _config.ModelName,
            temperature = 0.0,
            messages = new[]
            {
                new { role = "system", content = _config.SystemPrompt },
                new { role = "user", content = userContent }
            }
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _config.EndpointUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        requestMessage.Headers.Add("HTTP-Referer", "https://github.com/Ian-J-Harkin/ActuarialXL");
        requestMessage.Headers.Add("X-Title", "ActuarialXL Translation Engine");
        requestMessage.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ActuarialLlmBridgeException($"OpenRouter API returned {response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        
        try
        {
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var content = jsonDocument.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (content == null)
            {
                throw new ActuarialLlmBridgeException("LLM returned an empty content string.");
            }

            return ParseLlmOutput(content);
        }
        catch (JsonException ex)
        {
            throw new ActuarialLlmBridgeException("Failed to parse LLM JSON response.", ex);
        }
    }

    private TranslationOutput ParseLlmOutput(string rawOutput)
    {
        var delimiter = "===CSHARP_MIRROR===";
        var parts = rawOutput.Split(new[] { delimiter }, StringSplitOptions.None);

        if (parts.Length != 2)
        {
            throw new ActuarialLlmBridgeException($"LLM output did not contain exactly one delimiter '{delimiter}'. Found {parts.Length - 1}.");
        }

        var markdown = parts[0].Trim();
        var code = parts[1].Trim();

        // Defensive extraction: In case the LLM wrapped the code in markdown blocks like ```csharp ... ```
        var codeBlockRegex = new Regex(@"```(?:csharp|cs)?\s+(.*?)\s+```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var match = codeBlockRegex.Match(code);
        if (match.Success)
        {
            code = match.Groups[1].Value.Trim();
        }

        // Add the mandated wrappers from the spec
        var finalCode = $$"""
            using System;
            using System.Collections.Generic;
            using ActuarialTranslationEngine.Core.Interfaces;

            {{code}}
            """;

        return new TranslationOutput
        {
            FinalAuditableMarkdown = markdown,
            GeneratedCSharpMirrorCode = finalCode
        };
    }
}
