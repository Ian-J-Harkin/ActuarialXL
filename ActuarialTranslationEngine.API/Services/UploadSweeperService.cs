using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ActuarialTranslationEngine.API.Services;

public class UploadSweeperService : BackgroundService
{
    private readonly ILogger<UploadSweeperService> _logger;
    private readonly TimeSpan _sweepInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _fileMaxAge = TimeSpan.FromHours(24);

    public UploadSweeperService(ILogger<UploadSweeperService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Upload Sweeper Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (Directory.Exists(uploadDir))
                {
                    var files = Directory.GetFiles(uploadDir, "*.xlsx");
                    var cutoffTime = DateTime.UtcNow.Subtract(_fileMaxAge);
                    int deletedCount = 0;

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTimeUtc < cutoffTime)
                        {
                            try
                            {
                                fileInfo.Delete();
                                deletedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete old upload file: {File}", file);
                            }
                        }
                    }

                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Upload Sweeper deleted {Count} abandoned files older than {Hours} hours.", deletedCount, _fileMaxAge.TotalHours);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Upload Sweeper execution.");
            }

            await Task.Delay(_sweepInterval, stoppingToken);
        }

        _logger.LogInformation("Upload Sweeper Service is stopping.");
    }
}
