using Microsoft.AspNetCore.SignalR;
using Misfitz_Games.Hubs;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services.Room;

public sealed class RoomBroadcastService(
    IHubContext<RoomHub> hub,
    IRoomStateStore store,
    ILogger<RoomBroadcastService> log
)
{
    private static string GroupName(Guid roomId) => $"room:{roomId:D}";

    // ------------------------------------------------------------
    // Core: broadcast the latest stored state
    // ------------------------------------------------------------
    public async Task BroadcastStateAsync(Guid roomId, CancellationToken ct = default)
    {
        var state = await store.GetStateAsync(roomId, ct);
        if (state is null) return;

        await BroadcastStateAsync(roomId, state, ct);
    }

    // ------------------------------------------------------------
    // Overload: broadcast a provided RoomState (no store read)
    // (Fixes: calls that pass 3 args: (roomId, state, ct))
    // ------------------------------------------------------------
    public Task BroadcastStateAsync(Guid roomId, RoomState state, CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("RoomStateUpdated", new
        {
            ok = true,
            roomId,
            utc = DateTimeOffset.UtcNow,
            state
        }, ct);

    // ------------------------------------------------------------
    // Overload: broadcast arbitrary payload as a "RoomStateUpdated"
    // This unblocks test controllers / misc publishers that send anon payloads.
    // (Fixes: TestBroadcastController + any (roomId, anon, ct) calls)
    // ------------------------------------------------------------
    public Task BroadcastStateAsync(Guid roomId, object payload, CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("RoomStateUpdated", new
        {
            ok = true,
            roomId,
            utc = DateTimeOffset.UtcNow,
            payload
        }, ct);

    // ------------------------------------------------------------
    // Toast helper (Fixes: ToastAsync missing in multiple files)
    // ------------------------------------------------------------
    public Task ToastAsync(Guid roomId, string message, string level = "info", CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("Toast", new
        {
            ok = true,
            roomId,
            level,
            message,
            utc = DateTimeOffset.UtcNow
        }, ct);

    // ------------------------------------------------------------
    // Room closed helper (Fixes: BroadcastRoomClosedAsync missing)
    // ------------------------------------------------------------
    public Task BroadcastRoomClosedAsync(Guid roomId, string? reason = null, CancellationToken ct = default)
        => hub.Clients.Group(GroupName(roomId)).SendAsync("RoomClosed", new
        {
            ok = true,
            roomId,
            reason,
            utc = DateTimeOffset.UtcNow
        }, ct);
}