using System.Net.Http.Headers;
using System.Text.Json;
using ActuarialTranslationEngine.Core.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace ActuarialTranslationEngine.Web.Services;

public class ApiTranslationResponse
{
    public string? WorkbookName { get; set; }
    public string? WorksheetName { get; set; }
    public Guid TranslationId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ModelUsed { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<TranslationOutput> Evaluations { get; set; } = new();
}

public class HistorySummary
{
    public Guid Id { get; set; }
    public string? OriginalFileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int EvaluationsCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class JobEnqueueResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SessionCreateResponse
{
    public Guid SessionId { get; set; }
    public List<JobSummary> Jobs { get; set; } = new();
}

public class JobSummary
{
    public Guid Id { get; set; }
    public string TargetSheet { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ApiTranslationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiTranslationClient> _logger;

    public ApiTranslationClient(HttpClient httpClient, ILogger<ApiTranslationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Guid> EvaluateFileAsync(IBrowserFile file, string targetSheet = "ALL", string? connectionId = null)
    {
        string correlationId = Guid.NewGuid().ToString();
        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        _logger.LogInformation("Initiating translation upload with CorrelationId: {CorrelationId}", correlationId);

        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
        using var fileContent = new StreamContent(fileStream);
        
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", file.Name);

        if (!string.IsNullOrEmpty(connectionId))
        {
            content.Add(new StringContent(connectionId), "connectionId");
        }
        content.Add(new StringContent(correlationId), "correlationId");
        content.Add(new StringContent(targetSheet), "targetSheet");

        var response = await _httpClient.PostAsync("/api/evaluate", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"API Error {(int)response.StatusCode}: {errorBody}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JobEnqueueResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result?.JobId ?? Guid.Empty;
    }

    public async Task<SessionCreateResponse> CreateSessionAsync(IBrowserFile file, string targetSheet = "ALL")
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", file.Name);
        content.Add(new StringContent(targetSheet), "targetSheet");

        var response = await _httpClient.PostAsync("/api/session/create", content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"API Error: {await response.Content.ReadAsStringAsync()}");

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SessionCreateResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public async Task ExecuteSessionJobAsync(Guid sessionId, Guid jobId, string? connectionId = null)
    {
        var dict = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(connectionId))
            dict["connectionId"] = connectionId;

        using var content = new FormUrlEncodedContent(dict);

        var response = await _httpClient.PostAsync($"/api/session/{sessionId}/execute/{jobId}", content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"API Error: {await response.Content.ReadAsStringAsync()}");
    }

    public async Task<List<HistorySummary>> GetHistoryAsync(int skip = 0, int take = 10)
    {
        return await _httpClient.GetFromJsonAsync<List<HistorySummary>>($"/api/history?skip={skip}&take={take}") 
            ?? new List<HistorySummary>();
    }

    public async Task<ApiTranslationResponse?> GetHistoricalTranslationAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"/api/history/{id}");
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiTranslationResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
