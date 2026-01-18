using Microsoft.AspNetCore.SignalR;
using Misfitz_Games.Hubs;

namespace Misfitz_Games.Services;

public sealed class RoomBroadcastService(IHubContext<RoomHub> hub)
{
    private static string GroupName(Guid roomId) => $"room:{roomId:D}";

    public Task BroadcastStateAsync(Guid roomId, object state, CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("StateUpdated", state, ct);

    public Task ToastAsync(Guid roomId, string message, CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("Toast", message, ct);
}