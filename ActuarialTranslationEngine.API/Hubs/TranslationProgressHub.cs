using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ActuarialTranslationEngine.API.Hubs;

public class TranslationProgressHub : Hub
{
    // Maps JobId -> Timestamp of disconnect. If null, client is connected.
    public static ConcurrentDictionary<Guid, DateTime?> JobDisconnects { get; } = new();
    
    // Maps ConnectionId -> JobId
    public static ConcurrentDictionary<string, Guid> ConnectionToJob { get; } = new();

    public async Task JoinJobGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
        if (Guid.TryParse(jobId, out var parsedId))
        {
            ConnectionToJob[Context.ConnectionId] = parsedId;
            JobDisconnects[parsedId] = null; // Clear disconnect timestamp
        }
    }

    public async Task LeaveJobGroup(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionToJob.TryRemove(Context.ConnectionId, out var jobId))
        {
            JobDisconnects[jobId] = DateTime.UtcNow;
        }
        return base.OnDisconnectedAsync(exception);
    }
}

