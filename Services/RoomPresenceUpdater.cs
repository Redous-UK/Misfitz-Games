using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public static class RoomPresenceUpdater
{
    public static RoomState TouchPlayer(RoomState state, string userId, string username, bool isConnected = true)
    {
        var now = DateTimeOffset.UtcNow;

        // ✅ clone list so we don't mutate the existing record's list instance
        var players = state.Players is null
            ? new List<PlayerPresence>()
            : new List<PlayerPresence>(state.Players);

        var idx = players.FindIndex(p => p.UserId == userId);

        if (idx >= 0)
        {
            var cur = players[idx];
            players[idx] = cur with
            {
                Name = string.IsNullOrWhiteSpace(username) ? cur.Name : username,
                LastSeenUtc = now,
                IsConnected = isConnected
            };
        }
        else
        {
            players.Add(new PlayerPresence(
                UserId: userId,
                Name: string.IsNullOrWhiteSpace(username) ? "Player" : username,
                LastSeenUtc: now,
                IsConnected: isConnected,
                IsReady: false,
                AvatarUrl: null
            ));
        }

        var host = string.IsNullOrWhiteSpace(state.HostUserId) ? userId : state.HostUserId;

        return state with
        {
            Players = players,
            HostUserId = host,
            UpdatedAtUtc = now
        };
    }
}
