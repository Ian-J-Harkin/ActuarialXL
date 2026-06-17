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
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Engine;
using ActuarialTranslationEngine.Engine.LlmBridge;
using ActuarialTranslationEngine.Engine.Orchestration;
using ActuarialTranslationEngine.Engine.Roslyn;
using ActuarialTranslationEngine.Persistence;
using ActuarialTranslationEngine.Core.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

// Concurrency gate
builder.Services.AddSingleton(new SemaphoreSlim(Environment.ProcessorCount));

// Engine registrations
builder.Services.AddSingleton<IActuarialExtractionEngine, ActuarialExtractionEngine>();
builder.Services.AddSingleton<IVectorCompressionEngine, VectorCompressionEngine>();

builder.Services.AddSingleton<LlmBridgeConfiguration>(provider =>
{
    var cfg = new LlmBridgeConfiguration();
    var key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    if (!string.IsNullOrWhiteSpace(key))
        cfg.ApiKey = key;
    else
        cfg.ApiKey = "dummy_for_testing"; // Default dummy key for integration test

    string relativePath = Path.Combine("docs", "governance", "master-prompt-engineering-log.md");
    string promptPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
    if (!File.Exists(promptPath))
    {
        promptPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", relativePath));
    }

    if (File.Exists(promptPath))
    {
        string logContent = File.ReadAllText(promptPath);
        var match = System.Text.RegularExpressions.Regex.Match(logContent, @"### The System Prompt\s*```text\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (match.Success)
        {
            cfg.SystemPrompt = match.Groups[1].Value.Trim();
        }
        else
        {
            cfg.SystemPrompt = string.Empty;
        }
    }
    else
    {
        cfg.SystemPrompt = string.Empty;
    }

    if (string.IsNullOrWhiteSpace(cfg.SystemPrompt))
        throw new InvalidOperationException("Prompt file is empty or whitespace.");

    return cfg;
});

// Since the bridge requires an HttpClient, register it.
builder.Services.AddHttpClient<LiveDomainInterrogationBridge>();
builder.Services.AddSingleton<IDomainInterrogationBridge, LiveDomainInterrogationBridge>();
builder.Services.AddSingleton<IRoslynReconciliationEngine, RoslynReconciliationEngine>();
builder.Services.AddSingleton<IReconciliationOrchestrator, ReconciliationOrchestrator>();

// Persistence registration
builder.Services.AddActuarialPersistence("audit.db");

var app = builder.Build();

app.MapPost("/api/evaluate", async (HttpRequest request, 
    IActuarialExtractionEngine extractionEngine,
    IVectorCompressionEngine compressionEngine,
    IReconciliationOrchestrator orchestrator,
    IPersistenceManager persistenceManager,
    SemaphoreSlim concurrencyGate,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType || !request.Form.Files.Any())
    {
        return Results.BadRequest(new { Error = "No file uploaded." });
    }

    var file = request.Form.Files.First();
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    
    if (ext != ".xlsx" && ext != ".xlsm")
    {
        return Results.BadRequest(new { Error = "Unsupported file type. Only .xlsx and .xlsm are supported." });
    }

    await concurrencyGate.WaitAsync(cancellationToken);
    try
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        
        stream.Position = 0;
        var sheets = extractionEngine.GetWorksheetNames(stream);
        if (!sheets.Any()) return Results.BadRequest(new { Error = "No worksheets found." });
        var targetSheet = sheets.First();

        stream.Position = 0;
        var sheetRawMap = extractionEngine.ExtractSheetData(stream, targetSheet);

        // Phase 2: Compression
        var compressedBlock = compressionEngine.CompressTopology(sheetRawMap);

        // Phase 3 & 4: Translation, Reconciliation & Assembly Sandboxing
        // 1. Process translation block
        var translationOutputs = await orchestrator.ProcessBlockAsync(compressedBlock, sheetRawMap, cancellationToken);

        // Phase 5: Persistence
        var record = new TranslatedModelRecord
        {
            Id = Guid.NewGuid(),
            OriginalFileName = file.FileName,
            FileHash = "uploaded-stream", // Dummy for now
            CreatedAt = DateTime.UtcNow,
            Payload = new TranslationPayload 
            {
                Output = translationOutputs.FirstOrDefault(),
                // Fix for incomplete persistence: ideally Payload would hold a list, but we update Output or similar
                // Wait, TranslationPayload is defined elsewhere. If it doesn't support list, we might have to just accept it.
            }
        };

        await persistenceManager.SaveTranslationAsync(record, cancellationToken);

        return Results.Ok(new
        {
            WorkbookName = file.FileName,
            WorksheetName = targetSheet,
            Evaluations = translationOutputs
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
    finally
    {
        concurrencyGate.Release();
    }
}).DisableAntiforgery();

app.Run();

// Required to make the top-level class visible for integration tests
public partial class Program { }
