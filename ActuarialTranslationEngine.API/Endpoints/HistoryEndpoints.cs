using System;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ActuarialTranslationEngine.Core.Interfaces;
using ActuarialTranslationEngine.Core.Models;
using ActuarialTranslationEngine.Core.Persistence;

namespace ActuarialTranslationEngine.API.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/history", async (IPersistenceManager persistenceManager, int skip = 0, int take = 10, CancellationToken cancellationToken = default) =>
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 50);
            var history = await persistenceManager.GetPaginatedHistoryAsync(skip, take, cancellationToken);
            return Results.Ok(history.Select(h => new
            {
                h.Id,
                h.OriginalFileName,
                h.TargetSheet,
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
                WorksheetName = job.TargetSheet,
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
    }
}
