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

    public async Task<TranslationOutput> ProcessPayloadAsync(CompressedVectorBlock payload, string targetColumn, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        
        var userContent = $"Payload: \n{payloadJson}\n\nCRITICAL: Your ExecuteCalculationRow method MUST return the calculated value for column '{targetColumn}'. Do not return the value for any other column.";
        
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

        if (_config.ApiKey == "dummy_for_testing")
        {
            var mockCSharp = @"using System; 
using System.Collections.Generic; 
using ActuarialTranslationEngine.Core.Interfaces; 

public class DynamicReconciliationUnit : IActuarialReconciliationUnit 
{ 
    public decimal ExecuteCalculationRow(IDictionary<string, decimal> inputs) => 0m; 
}";
            return ParseLlmOutput($"This is a dummy test output.\n===CSHARP_MIRROR===\n{mockCSharp}");
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _config.EndpointUrl);
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        requestMessage.Headers.Add("HTTP-Referer", "https://github.com/Ian-J-Harkin/ActuarialXL");
        requestMessage.Headers.Add("X-Title", "ActuarialXL Translation Engine");
        requestMessage.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ActuarialLlmBridgeException($"LLM API returned {response.StatusCode}: {errorBody}");
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

    public async Task<TranslationOutput> ProcessVbaPayloadAsync(VbaModuleCode payload, string? previousCompilerError = null, CancellationToken cancellationToken = default)
    {
        var userContent = $"VBA Source Code for module '{payload.ModuleName}':\n```vba\n{payload.RawVbaTextString}\n```";
        
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
                new { role = "system", content = string.IsNullOrEmpty(_config.VbaSystemPrompt) ? _config.SystemPrompt : _config.VbaSystemPrompt },
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
            throw new ActuarialLlmBridgeException($"LLM API returned {response.StatusCode}: {errorBody}");
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
        // Parsing contract — four paths in priority order:
        //
        //  Path 1 (Delimiter + markdown block):  delimiter present, code in ```csharp``` fence.
        //  Path 2 (Delimiter + raw C#):          delimiter present, code is bare (no fence).
        //  Path 3 (No delimiter + has class):    Postel's Law — LLM forgot delimiter but included
        //                                         a recognisable class; extract it gracefully.
        //  Path 4 (No delimiter + no class):     Unrecoverable — throw ActuarialLlmBridgeException.
        //
        var delimiter = "===CSHARP_MIRROR===";
        var parts = rawOutput.Split(new[] { delimiter }, StringSplitOptions.None);

        string markdown = "";
        string code;
        bool delimiterFound = parts.Length == 2;

        if (delimiterFound)
        {
            // Paths 1 & 2: delimiter present
            markdown = parts[0].Trim();
            code = parts[1].Trim();
        }
        else
        {
            // Paths 3 & 4: no delimiter — apply Postel's Law only if a class exists
            code = rawOutput;
        }

        // Defensive extraction: In case the LLM wrapped the code in a ```csharp ... ``` fence
        var codeBlockRegex = new Regex(@"```(?:csharp|cs)?\s+(.*?)\s+```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var match = codeBlockRegex.Match(code);
        if (match.Success)
        {
            code = match.Groups[1].Value.Trim();
        }
        else
        {
            // Strip any residual markdown delimiters
            code = code.Replace("```csharp", "").Replace("```cs", "").Replace("```", "");

            // Drop conversational preamble by anchoring on the first class declaration
            int classIndex = code.IndexOf("public class", StringComparison.Ordinal);
            if (classIndex > 0)
            {
                code = code.Substring(classIndex);
            }

            code = code.Trim();
        }

        // Path 4 guard: if no delimiter was present AND we still have no recognisable C# class,
        // the response is unrecoverable — throw rather than silently pass garbage to Roslyn.
        if (!delimiterFound && !code.Contains("public class", StringComparison.Ordinal))
        {
            throw new ActuarialLlmBridgeException(
                $"LLM response contained neither the '{delimiter}' delimiter nor a recognisable C# class declaration. " +
                $"Raw response: {rawOutput.Substring(0, Math.Min(200, rawOutput.Length))}");
        }

        // Add the mandated namespace imports from the spec
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
