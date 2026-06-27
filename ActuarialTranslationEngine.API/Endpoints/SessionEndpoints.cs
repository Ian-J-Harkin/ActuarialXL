using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.API.Services;

namespace ActuarialTranslationEngine.API.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/session/upload", async (HttpRequest request, IActuarialExtractionEngine extractionEngine, IPersistenceManager persistenceManager) =>
        {
            try
            {
                if (!request.HasFormContentType || !request.Form.Files.Any()) return Results.BadRequest(new { Error = "No file uploaded." });
                var file = request.Form.Files.First();
                
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext != ".xlsx" && ext != ".xlsm") return Results.BadRequest(new { Error = "Unsupported file type. Only .xlsx and .xlsm are supported." });

                if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { Error = "File exceeds the 5MB upload limit." });

                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                
                var sessionId = Guid.NewGuid();
                var tempPath = Path.Combine(uploadDir, $"{sessionId}.xlsx");

                try
                {
                    using (var fileStream = new FileStream(tempPath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }

                    byte[] magicBytes = new byte[2];
                    using (var readStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        readStream.Read(magicBytes, 0, 2);
                    }
                    if (magicBytes[0] != 0x50 || magicBytes[1] != 0x4B)
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                        return Results.BadRequest(new { Error = "Invalid file signature. File is not a valid ZIP/XLSX." });
                    }

                    List<string> sheets;
                    using (var readStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        sheets = extractionEngine.GetWorksheetNames(readStream);
                    }
                    
                    var jobId = Guid.NewGuid();
                    var job = await persistenceManager.CreateJobAsync(jobId, file.FileName, "uploaded-stream", "Live Model", "Pending Selection", sessionId);

                    return Results.Ok(new { SessionId = sessionId, AvailableSheets = sheets });
                }
                catch (Exception)
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/session/configure", async (HttpRequest request, IActuarialExtractionEngine extractionEngine, IPersistenceManager persistenceManager) =>
        {
            try
            {
                if (!request.HasJsonContentType()) return Results.BadRequest(new { Error = "Expected JSON payload." });
                var payload = await request.ReadFromJsonAsync<ConfigureSessionRequest>();
                if (payload == null || payload.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(payload.TargetSheet))
                {
                    return Results.BadRequest(new { Error = "Invalid payload. SessionId and TargetSheet are required." });
                }

                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                var filePath = Path.Combine(uploadDir, $"{payload.SessionId}.xlsx");
                
                if (!File.Exists(filePath))
                    return Results.NotFound(new { Error = "Session file not found or already deleted. Please re-upload the file." });

                if (payload.TargetSheet == "ALL")
                {
                    return Results.BadRequest(new { Error = "The 'ALL' strategy has been disabled. You must select a specific target sheet." });
                }

                List<string> sheets;
                using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    sheets = extractionEngine.GetWorksheetNames(readStream);
                }

                if (!sheets.Contains(payload.TargetSheet))
                {
                    return Results.BadRequest(new { Error = $"The requested target sheet '{payload.TargetSheet}' does not exist in the workbook." });
                }

                var jobs = await persistenceManager.GetJobsBySessionIdAsync(payload.SessionId);
                var job = jobs.FirstOrDefault();
                if (job == null) return Results.NotFound(new { Error = "Session job record not found." });

                await persistenceManager.UpdateJobTargetSheetAsync(job.Id, payload.TargetSheet);

                return Results.Ok(new { SessionId = payload.SessionId, Jobs = new[] { new { job.Id, TargetSheet = payload.TargetSheet, Status = job.Status.ToString() } } });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        app.MapPost("/api/session/{sessionId:guid}/execute/{jobId:guid}", async (Guid sessionId, Guid jobId, HttpRequest request, ITranslationJobQueue jobQueue, IPersistenceManager persistenceManager) =>
        {
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var filePath = Path.Combine(uploadDir, $"{sessionId}.xlsx");
            
            if (!File.Exists(filePath))
                return Results.NotFound(new { Error = "Session file not found or already deleted. Please re-upload the file." });

            var job = await persistenceManager.GetJobDetailsAsync(jobId);
            if (job == null) return Results.NotFound();

            if (job.Status == TranslationJobStatus.Running || job.Status == TranslationJobStatus.Completed)
            {
                return Results.Conflict(new { Error = "Job is already running or completed and cannot be executed again." });
            }

            string? connectionId = null;
            if (request.HasFormContentType && request.Form.TryGetValue("connectionId", out var cid))
            {
                connectionId = cid.ToString();
            }
            var cts = new System.Threading.CancellationTokenSource();
            Program.ActiveJobTokens.TryAdd(jobId, cts);

            var jobRequest = new TranslationJobRequest
            {
                JobId = jobId,
                OriginalFileName = job.OriginalFileName,
                FilePath = filePath,
                TargetSheet = job.TargetSheet,
                ConnectionId = connectionId,
                CorrelationId = Guid.NewGuid().ToString(),
                CancellationToken = cts.Token
            };

            await jobQueue.EnqueueJobAsync(jobRequest, default);
            return Results.Accepted($"/api/history/{jobId}", new { JobId = jobId, Status = "Accepted" });
        }).DisableAntiforgery();
        
        app.MapGet("/api/session/{sessionId:guid}/jobs", async (Guid sessionId, IPersistenceManager persistenceManager) => 
        {
            var jobs = await persistenceManager.GetJobsBySessionIdAsync(sessionId);
            return Results.Ok(new { SessionId = sessionId, Jobs = jobs.Select(j => new { j.Id, j.TargetSheet, Status = j.Status.ToString() }) });
        });

        app.MapPost("/api/session/{sessionId:guid}/finish", async (Guid sessionId, IPersistenceManager persistenceManager) => 
        {
            var jobs = await persistenceManager.GetJobsBySessionIdAsync(sessionId);
            if (jobs.Any(j => j.Status == TranslationJobStatus.Pending || j.Status == TranslationJobStatus.Running))
            {
                return Results.BadRequest(new { Error = "Cannot delete session file while jobs are still Pending or Running." });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var filePath = Path.Combine(uploadDir, $"{sessionId}.xlsx");
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return Results.Ok(new { Message = "Session file cleaned up successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to clean up session file: {ex.Message}");
            }
        });
    }
}

public class ConfigureSessionRequest
{
    public Guid SessionId { get; set; }
    public string TargetSheet { get; set; } = string.Empty;
}
