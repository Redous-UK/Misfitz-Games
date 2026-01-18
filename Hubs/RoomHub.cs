using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Misfitz_Games.Hubs;

public class RoomHub(ILogger<RoomHub> log) : Hub
{
    private static string GroupName(Guid roomId) => $"room:{roomId:D}";

    public override Task OnConnectedAsync()
    {
        log.LogInformation("SignalR connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        log.LogInformation("SignalR disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client calls this to subscribe to updates for a room.
    /// </summary>
    public async Task JoinRoom(Guid roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
        log.LogInformation("Connection {ConnectionId} joined {Group}", Context.ConnectionId, GroupName(roomId));

        await Clients.Caller.SendAsync("JoinedRoom", new
        {
            ok = true,
            roomId,
            utc = DateTimeOffset.UtcNow
        });
    }

    public async Task LeaveRoom(Guid roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));
        log.LogInformation("Connection {ConnectionId} left {Group}", Context.ConnectionId, GroupName(roomId));

        await Clients.Caller.SendAsync("LeftRoom", new
        {
            ok = true,
            roomId,
            utc = DateTimeOffset.UtcNow
        });
    }
}