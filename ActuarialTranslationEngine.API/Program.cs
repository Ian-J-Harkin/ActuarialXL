using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Polly;
using ActuarialTranslationEngine.API;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Engine.LlmBridge;
using ActuarialTranslationEngine.Engine.Orchestration;
using ActuarialTranslationEngine.Engine.Roslyn;
using ActuarialTranslationEngine.Persistence;
using ActuarialTranslationEngine.Core.Persistence;
using Microsoft.AspNetCore.SignalR;
using ActuarialTranslationEngine.API.Hubs;
using ActuarialTranslationEngine.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Logging.AddSimpleConsole(options => 
{
    options.IncludeScopes = true;
});

// Concurrency gate
builder.Services.AddSingleton(new SemaphoreSlim(Environment.ProcessorCount));

// Engine registrations
builder.Services.AddSingleton<IActuarialExtractionEngine, ActuarialExtractionEngine>();
builder.Services.AddSingleton<IVectorCompressionEngine, VectorCompressionEngine>();
builder.Services.AddSingleton<IVbaExtractionEngine, VbaExtractionEngine>();

builder.Services.AddSingleton<LlmBridgeConfiguration>(provider =>
{
    var cfg = new LlmBridgeConfiguration();
    var key = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_API_KEY");
    if (!string.IsNullOrWhiteSpace(key))
        cfg.ApiKey = key; // Default dummy key for integration test

    var endpoint = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(endpoint))
        cfg.EndpointUrl = endpoint;

    string promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system-prompt.txt");
    if (File.Exists(promptPath))
    {
        cfg.SystemPrompt = File.ReadAllText(promptPath).Trim();
    }
    else
    {
        cfg.SystemPrompt = string.Empty;
    }

    if (string.IsNullOrWhiteSpace(cfg.SystemPrompt))
        throw new InvalidOperationException("Prompt file is empty or whitespace.");

    return cfg;
});

// Since the bridge requires an HttpClient, register it with Polly Retry
builder.Services.AddHttpClient<LiveDomainInterrogationBridge>(client => 
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
    .AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions
        .HandleTransientHttpError()
        .Or<System.IO.IOException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"\n[Polly] Transient network error detected. Retrying attempt {retryAttempt} in {timespan.TotalSeconds} seconds...\n");
            }));

builder.Services.AddSingleton<IDomainInterrogationBridge, LiveDomainInterrogationBridge>();
builder.Services.AddSingleton<IRoslynReconciliationEngine, RoslynReconciliationEngine>();
builder.Services.AddSingleton<IReconciliationOrchestrator, ReconciliationOrchestrator>();

// Background Job Services
builder.Services.AddSingleton<ITranslationJobQueue, TranslationJobQueue>();
builder.Services.AddHostedService<BackgroundTranslationWorker>();

// Persistence registration
builder.Services.AddActuarialPersistence("audit.db");
builder.Services.AddSignalR();

var app = builder.Build();
app.MapHub<TranslationProgressHub>("/progressHub");

using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ActuarialDbContext>>();
    using var dbContext = contextFactory.CreateDbContext();
    dbContext.Database.EnsureCreated();
}

app.MapSessionEndpoints();

app.MapPost("/api/evaluate", async (HttpRequest request, 
    ITranslationJobQueue jobQueue,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("EvaluateEndpoint");
    try
    {
        if (!request.HasFormContentType || !request.Form.Files.Any())
        {
            return Results.BadRequest(new { Error = "No file uploaded." });
        }

        var file = request.Form.Files.First();
        var fileName = file.FileName;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        
        if (ext != ".xlsx" && ext != ".xlsm")
        {
            return Results.BadRequest(new { Error = "Unsupported file type. Only .xlsx and .xlsm are supported." });
        }

        // Security: 5MB File Limit
        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.BadRequest(new { Error = "File exceeds the 5MB upload limit." });
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var fileData = memoryStream.ToArray();

        // Security: Magic Byte Check for ZIP/XLSX (PK = 50 4B)
        if (fileData.Length < 2 || fileData[0] != 0x50 || fileData[1] != 0x4B)
        {
            return Results.BadRequest(new { Error = "Invalid file signature. File is not a valid ZIP/XLSX." });
        }

        string? connectionId = request.Form.TryGetValue("connectionId", out var cid) ? cid.ToString() : null;
        string correlationId = request.Form.TryGetValue("correlationId", out var corid) ? corid.ToString() : Guid.NewGuid().ToString();
        string targetSheet = request.Form.TryGetValue("targetSheet", out var tSheet) ? tSheet.ToString() : "ALL";

        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        var jobId = Guid.NewGuid();

        // Create a dedicated CTS for this job
        var cts = new CancellationTokenSource();
        Program.ActiveJobTokens.TryAdd(jobId, cts);

        var jobRequest = new TranslationJobRequest
        {
            JobId = jobId,
            OriginalFileName = fileName,
            FileData = fileData,
            TargetSheet = targetSheet,
            ConnectionId = connectionId,
            CorrelationId = correlationId,
            CancellationToken = cts.Token
        };

        await jobQueue.EnqueueJobAsync(jobRequest, cancellationToken);

        logger.LogInformation($"Job {jobId} enqueued for file {fileName}. ConnectionId: {connectionId}");

        return Results.Accepted($"/api/history/{jobId}", new { JobId = jobId, Status = "Accepted" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to enqueue evaluation job.");
        return Results.BadRequest(new { Error = ex.Message });
    }
}).DisableAntiforgery();

app.MapDelete("/api/evaluate/{id:guid}", (Guid id, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("EvaluateEndpoint");
    if (Program.ActiveJobTokens.TryGetValue(id, out var cts))
    {
        cts.Cancel();
        logger.LogInformation($"Cancellation requested for Job {id}");
        return Results.Ok(new { Message = "Job cancellation requested." });
    }
    return Results.NotFound(new { Error = "Active job not found or already completed." });
});

app.MapGet("/api/history", async (IPersistenceManager persistenceManager, int skip = 0, int take = 10, CancellationToken cancellationToken = default) =>
{
    var history = await persistenceManager.GetPaginatedHistoryAsync(skip, take, cancellationToken);
    return Results.Ok(history.Select(h => new
    {
        h.Id,
        h.OriginalFileName,
        h.CreatedAt,
        Status = h.Status.ToString(),
        EvaluationsCount = h.Partitions?.Count ?? 0
    }));
});

app.MapGet("/api/history/{id:guid}", async (Guid id, IPersistenceManager persistenceManager, CancellationToken cancellationToken) =>
{
    var job = await persistenceManager.GetJobDetailsAsync(id, cancellationToken);
    if (job == null) return Results.NotFound();
    
    return Results.Ok(new
    {
        WorkbookName = job.OriginalFileName,
        WorksheetName = "Historical Record",
        Evaluations = job.Partitions?.Select(p => new TranslationOutput { 
            SourceName = p.SourceName,
            FinalAuditableMarkdown = p.FinalAuditableMarkdown, 
            GeneratedCSharpMirrorCode = p.GeneratedCSharpMirrorCode,
            IsCertified = p.IsCertified,
            VarianceDelta = p.VarianceDelta,
            ErrorMessage = p.ErrorMessage,
            DisruptiveNodes = string.IsNullOrEmpty(p.DisruptiveNodesJson) 
                ? new System.Collections.Generic.List<DisruptiveNode>() 
                : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<DisruptiveNode>>(p.DisruptiveNodesJson) ?? new System.Collections.Generic.List<DisruptiveNode>()
        }).ToList() ?? new System.Collections.Generic.List<TranslationOutput>(),
        TranslationId = job.Id,
        Timestamp = job.CreatedAt,
        ModelUsed = job.ModelUsed,
        Status = job.Status.ToString()
    });
});

app.Run();

// Required to make the top-level class visible for integration tests
public partial class Program 
{
    public static System.Collections.Concurrent.ConcurrentDictionary<Guid, CancellationTokenSource> ActiveJobTokens { get; } = new();
    public static System.Collections.Concurrent.ConcurrentDictionary<Guid, byte[]> ActiveSessions { get; } = new();
}
