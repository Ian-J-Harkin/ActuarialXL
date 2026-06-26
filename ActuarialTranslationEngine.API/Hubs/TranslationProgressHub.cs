using Microsoft.AspNetCore.SignalR;

namespace ActuarialTranslationEngine.API.Hubs;

public class TranslationProgressHub : Hub
{
    public async Task JoinJobGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    }

    public async Task LeaveJobGroup(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
    }
}
