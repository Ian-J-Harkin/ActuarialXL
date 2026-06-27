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
    public string OriginalFileName { get; set; } = string.Empty;
    public string TargetSheet { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int EvaluationsCount { get; set; }
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

public class UploadFileResponse
{
    public Guid SessionId { get; set; }
    public List<string> AvailableSheets { get; set; } = new();
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

    public async Task<SessionCreateResponse> ConfigureSessionAsync(Guid sessionId, string targetSheet)
    {
        var content = JsonContent.Create(new { SessionId = sessionId, TargetSheet = targetSheet });

        var response = await _httpClient.PostAsync("/api/session/configure", content);
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

    public async Task FinishSessionAsync(Guid sessionId)
    {
        var response = await _httpClient.PostAsync($"/api/session/{sessionId}/finish", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to finish session: {error}");
        }
    }

    public async Task<SessionCreateResponse?> GetSessionJobsAsync(Guid sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/session/{sessionId}/jobs");
        if (!response.IsSuccessStatusCode) return null;
        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SessionCreateResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<UploadFileResponse> UploadFileAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", file.Name);

        var response = await _httpClient.PostAsync("/api/session/upload", content);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"API Error: {await response.Content.ReadAsStringAsync()}");

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UploadFileResponse>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new UploadFileResponse();
    }

    public async Task CancelJobAsync(Guid jobId)
    {
        var response = await _httpClient.DeleteAsync($"/api/evaluate/{jobId}");
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to cancel job: {await response.Content.ReadAsStringAsync()}");
    }
}
