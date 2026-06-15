namespace ActuarialTranslationEngine.Core.Models;

public class LlmBridgeConfiguration
{
    public string EndpointUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
    public string ModelName { get; set; } = "mistralai/codestral-2508";
    public string ApiKey { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
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
