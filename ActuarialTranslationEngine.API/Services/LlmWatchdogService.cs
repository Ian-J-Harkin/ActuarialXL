using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.API.Hubs;

namespace ActuarialTranslationEngine.API.Services;

public class LlmWatchdogService : BackgroundService
{
    private readonly ILogger<LlmWatchdogService> _logger;

    public LlmWatchdogService(ILogger<LlmWatchdogService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LLM Watchdog Service started.");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                
                var abandonedJobs = TranslationProgressHub.JobDisconnects
                    .Where(kvp => kvp.Value.HasValue && kvp.Value.Value < cutoff)
                    .ToList();

                foreach (var job in abandonedJobs)
                {
                    if (Program.ActiveJobTokens.TryGetValue(job.Key, out var cts))
                    {
                        _logger.LogWarning($"Job {job.Key} has been abandoned for > 5 minutes. Canceling LLM compute.");
                        try { cts.Cancel(); } catch { }
                    }
                    
                    // Cleanup tracker
                    TranslationProgressHub.JobDisconnects.TryRemove(job.Key, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Watchdog Service");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
