using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;
using ActuarialTranslationEngine.API.Services;

namespace ActuarialTranslationEngine.API;

public static class ProgramExtensions
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/session/create", async (HttpRequest request, IActuarialExtractionEngine extractionEngine, IPersistenceManager persistenceManager) =>
        {
            try
            {
                if (!request.HasFormContentType || !request.Form.Files.Any()) return Results.BadRequest(new { Error = "No file uploaded." });
                var file = request.Form.Files.First();
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileData = memoryStream.ToArray();

                var sessionId = Guid.NewGuid();
                Program.ActiveSessions.TryAdd(sessionId, fileData);

                using var stream = new MemoryStream(fileData);
                var sheets = extractionEngine.GetWorksheetNames(stream);
                var allowedSheets = new[] { "Table 13.4", "Example 16.3 - Part 1", "Example 18.4", "Example 13.12" };
                
                var strategy = request.Form.TryGetValue("targetSheet", out var tSheet) ? tSheet.ToString() : "ALL";
                var targetSheets = new List<string>();
                
                if (strategy == "ALL") 
                {
                    targetSheets = allowedSheets.Where(s => sheets.Contains(s)).ToList();
                    if (!targetSheets.Any()) targetSheets.Add(sheets.First());
                }
                else
                {
                    targetSheets.Add(strategy);
                }

                var jobs = new List<TranslationJobEntity>();
                foreach (var sheet in targetSheets)
                {
                    var jobId = Guid.NewGuid();
                    var job = await persistenceManager.CreateJobAsync(jobId, file.FileName, "uploaded-stream", "Live Model", sheet, sessionId);
                    jobs.Add(job);
                }

                return Results.Ok(new { SessionId = sessionId, Jobs = jobs.Select(j => new { j.Id, j.TargetSheet, Status = j.Status.ToString() }) });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapPost("/api/session/{sessionId:guid}/execute/{jobId:guid}", async (Guid sessionId, Guid jobId, HttpRequest request, ITranslationJobQueue jobQueue, IPersistenceManager persistenceManager) =>
        {
            if (!Program.ActiveSessions.TryGetValue(sessionId, out var fileData))
                return Results.NotFound(new { Error = "Session data lost. Please re-upload the file." });

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
                FileData = fileData,
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
    }
}
