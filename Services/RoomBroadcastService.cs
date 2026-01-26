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

    public Task BroadcastRoomClosedAsync(Guid roomId, CancellationToken ct = default)
        // Assuming you already broadcast to group "room:{roomId}"
        => hub.Clients.Group($"room:{roomId:D}").SendAsync("RoomClosed", new
        {
            roomId,
            utc = DateTimeOffset.UtcNow
        }, ct);
}