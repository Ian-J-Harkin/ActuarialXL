using System;
using System.IO;
using System.Linq;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.Persistence;

namespace ActuarialTranslationEngine.API.Services;

public static class StartupSweeper
{
    public static void RunDatabaseSweeper(ActuarialDbContext dbContext)
    {
        var orphanedJobs = dbContext.TranslationJobs
            .Where(j => j.Status == TranslationJobStatus.Running || j.Status == TranslationJobStatus.Pending)
            .ToList();
        
        foreach (var job in orphanedJobs)
        {
            job.Status = TranslationJobStatus.Failed;
            var part = new TranslationPartitionEntity 
            { 
                JobId = job.Id, 
                PartitionIndex = 1, 
                ErrorMessage = "Job orphaned due to server restart.",
                FinalAuditableMarkdown = "",
                GeneratedCSharpMirrorCode = ""
            };
            dbContext.TranslationPartitions.Add(part);
        }
        
        if (orphanedJobs.Any())
        {
            dbContext.SaveChanges();
        }
    }

    public static void RunDiskSweeper(string uploadDir, DateTime cutoffTime)
    {
        if (Directory.Exists(uploadDir))
        {
            var files = Directory.GetFiles(uploadDir, "*.xlsx");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTimeUtc < cutoffTime)
                {
                    try { fileInfo.Delete(); } catch { /* ignore */ }
                }
            }
        }
    }
}
