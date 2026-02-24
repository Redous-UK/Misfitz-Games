using Microsoft.AspNetCore.SignalR;
using Misfitz_Games.Hubs;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services.Room;

public sealed class RoomBroadcastService(IHubContext<RoomHub> hub, IRoomStateStore store)
{
    private static string GroupName(Guid roomId) => $"room:{roomId:D}";

    public async Task BroadcastStateAsync(Guid roomId, CancellationToken ct = default)
    {
        var state = await store.GetStateAsync(roomId, ct);
        if (state is null) return;

        await hub.Clients.Group(GroupName(roomId)).SendAsync("RoomStateUpdated", new
        {
            ok = true,
            roomId,
            utc = DateTimeOffset.UtcNow,
            state
        }, ct);
    }
}