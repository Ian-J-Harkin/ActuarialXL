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
        app.MapPost("/api/session/inspect", async (HttpRequest request, IActuarialExtractionEngine extractionEngine) =>
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
                var tempPath = Path.Combine(uploadDir, $"inspect_{Guid.NewGuid()}.xlsx");

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
                        return Results.BadRequest(new { Error = "Invalid file signature. File is not a valid ZIP/XLSX." });
                    }

                    List<string> sheets;
                    using (var readStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        sheets = extractionEngine.GetWorksheetNames(readStream);
                    }
                    return Results.Ok(sheets);
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/session/create", async (HttpRequest request, IActuarialExtractionEngine extractionEngine, IPersistenceManager persistenceManager) =>
        {
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var sessionId = Guid.NewGuid();
            var filePath = Path.Combine(uploadDir, $"{sessionId}.xlsx");

            try
            {
                if (!request.HasFormContentType || !request.Form.Files.Any()) return Results.BadRequest(new { Error = "No file uploaded." });
                var file = request.Form.Files.First();

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext != ".xlsx" && ext != ".xlsm") return Results.BadRequest(new { Error = "Unsupported file type. Only .xlsx and .xlsm are supported." });

                if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { Error = "File exceeds the 5MB upload limit." });

                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                byte[] magicBytes = new byte[2];
                using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    readStream.Read(magicBytes, 0, 2);
                }
                if (magicBytes[0] != 0x50 || magicBytes[1] != 0x4B)
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    return Results.BadRequest(new { Error = "Invalid file signature. File is not a valid ZIP/XLSX." });
                }

                List<string> sheets;
                using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    sheets = extractionEngine.GetWorksheetNames(readStream);
                }
                
                var strategy = request.Form.TryGetValue("targetSheet", out var tSheet) ? tSheet.ToString() : "";
                if (string.IsNullOrWhiteSpace(strategy) || strategy == "ALL")
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    return Results.BadRequest(new { Error = "The 'ALL' strategy has been disabled. You must select a specific target sheet." });
                }

                if (!sheets.Contains(strategy))
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    return Results.BadRequest(new { Error = $"The requested target sheet '{strategy}' does not exist in the workbook." });
                }

                var jobs = new List<TranslationJobEntity>();
                var jobId = Guid.NewGuid();
                var job = await persistenceManager.CreateJobAsync(jobId, file.FileName, "uploaded-stream", "Live Model", strategy, sessionId);
                jobs.Add(job);

                return Results.Ok(new { SessionId = sessionId, Jobs = jobs.Select(j => new { j.Id, j.TargetSheet, Status = j.Status.ToString() }) });
            }
            catch (Exception ex)
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/session/{sessionId:guid}/execute/{jobId:guid}", async (Guid sessionId, Guid jobId, HttpRequest request, ITranslationJobQueue jobQueue, IPersistenceManager persistenceManager) =>
        {
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            var filePath = Path.Combine(uploadDir, $"{sessionId}.xlsx");
            
            if (!File.Exists(filePath))
                return Results.NotFound(new { Error = "Session file not found or already deleted. Please re-upload the file." });

            var job = await persistenceManager.GetJobDetailsAsync(jobId);
            if (job == null) return Results.NotFound();

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

        app.MapPost("/api/session/{sessionId:guid}/finish", (Guid sessionId) => 
        {
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
