using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public static class RoomPresenceUpdater
{
    public static RoomState TouchPlayer(RoomState state, string userId, string username, bool isConnected = true)
    {
        var players = state.Players ?? new List<PlayerPresence>();

        var now = DateTimeOffset.UtcNow;

        // find existing
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

        // set host if none
        var host = state.HostUserId;
        if (string.IsNullOrWhiteSpace(host))
            host = userId;

        return state with
        {
            Players = players,
            HostUserId = host,
            UpdatedAtUtc = now
        };
    }
}