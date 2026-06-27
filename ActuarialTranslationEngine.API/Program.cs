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
using Microsoft.Extensions.Options;
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
using ActuarialTranslationEngine.API.Endpoints;

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

builder.Services.Configure<LlmBridgeConfiguration>(builder.Configuration.GetSection("LlmBridge"));
builder.Services.PostConfigure<LlmBridgeConfiguration>(cfg =>
{
    var key = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_API_KEY");
    if (!string.IsNullOrWhiteSpace(key))
        cfg.ApiKey = key;

    var endpoint = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(endpoint))
        cfg.EndpointUrl = endpoint;

    string promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system-prompt.txt");
    if (File.Exists(promptPath))
        cfg.SystemPrompt = File.ReadAllText(promptPath).Trim();
    else
        cfg.SystemPrompt = string.Empty;

    if (string.IsNullOrWhiteSpace(cfg.SystemPrompt))
        throw new InvalidOperationException("Prompt file is empty or whitespace.");
});

builder.Services.AddSingleton<LlmBridgeConfiguration>(provider => 
    provider.GetRequiredService<IOptions<LlmBridgeConfiguration>>().Value);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddHostedService<LlmWatchdogService>();
builder.Services.AddHostedService<UploadSweeperService>();

// Persistence registration
var dbPath = Environment.GetEnvironmentVariable("ACTUARIAL_DB_PATH") ?? "audit.db";
builder.Services.AddActuarialPersistence(dbPath);
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHub<TranslationProgressHub>("/progressHub");

using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ActuarialDbContext>>();
    using var dbContext = contextFactory.CreateDbContext();
    dbContext.Database.Migrate();
    
    // Sweeper logic: mark orphaned jobs from previous crashes as Failed
    StartupSweeper.RunDatabaseSweeper(dbContext);
    
    // Disk Sweeper: delete any .xlsx files in uploads/ older than 24 hours
    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    StartupSweeper.RunDiskSweeper(uploadDir, DateTime.UtcNow.AddHours(-24));
}

app.MapSessionEndpoints();
app.MapEvaluateEndpoints();
app.MapHistoryEndpoints();

app.Run();

// Required to make the top-level class visible for integration tests
public partial class Program 
{
    public static System.Collections.Concurrent.ConcurrentDictionary<Guid, CancellationTokenSource> ActiveJobTokens { get; } = new();
}
