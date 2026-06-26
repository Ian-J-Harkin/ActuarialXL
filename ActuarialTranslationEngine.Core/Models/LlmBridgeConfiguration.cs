namespace ActuarialTranslationEngine.Core.Models;

public class LlmBridgeConfiguration
{
    // DEFAULT: We use OpenRouter to route requests to Codestral.
    // IF YOU WANT TO USE MISTRAL DIRECTLY AT A LATER DATE:
    // 1. Set the environment variable ACTUARIAL_LLM_ENDPOINT to "https://api.mistral.ai/v1/chat/completions"
    // 2. Set the environment variable ACTUARIAL_LLM_API_KEY to a native Mistral API key. 
    //    WARNING: A native Mistral API key must NOT be an OpenRouter key (e.g., do not use a key starting with "sk-or-v1-")
    // 3. You may also need to change the ModelName below to a native Mistral model string (e.g., "codestral-latest")
    public string EndpointUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string ModelName { get; set; } = "mistralai/codestral-2508";
    public string ApiKey { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string VbaSystemPrompt { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 2000;
    
    public List<string> AllowedNamespaces { get; set; } = new()
    {
        "System",
        "System.Collections.Generic",
        "ActuarialTranslationEngine.Core.Interfaces"
    };

    public List<string> ForbiddenNamespaces { get; set; } = new()
    {
        "System.IO",
        "System.Net",
        "System.Diagnostics",
        "System.Reflection",
        "System.Runtime.InteropServices",
        "System.Threading"
    };
}
