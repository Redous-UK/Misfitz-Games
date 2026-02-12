using Misfitz_Games.Models;
using System.Text.Json;

namespace Misfitz_Games.Services.Room;

public static class RoomStateProjector
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static object ToPublic(RoomState state)
    {
        // --- Ensure players key always exists (never null) ---
        var players = state.Players ?? [];

        // Build a userId -> username lookup (players first, then recent guesses)
        var nameByUserId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in players)
            if (!string.IsNullOrWhiteSpace(p.UserId) && !string.IsNullOrWhiteSpace(p.Name))
                nameByUserId[p.UserId] = p.Name;

        // --- Normalize contexto state regardless of storage type (ContextoState or JsonElement) ---
        ContextoState? cs = null;

        if (state.ActiveGame == GameType.Contexto)
        {
            if (state.GameState is ContextoState direct)
                cs = direct;
            else if (state.GameState is JsonElement je)
            {
                try { cs = je.Deserialize<ContextoState>(JsonOpts); } catch { /* ignore */ }
            }
        }

        // Fill gaps from recent guesses (last-known username)
        if (cs is not null)
        {
            foreach (var g in cs.RecentGuesses)
                if (!string.IsNullOrWhiteSpace(g.UserId) && !string.IsNullOrWhiteSpace(g.Username))
                    nameByUserId.TryAdd(g.UserId, g.Username);
        }

        // If contexto, strip SecretWord from the public output
        object? publicGameState = state.GameState;

        if (cs is not null)
        {
            // Build username-aware leaderboard
            var leaderboard = cs.ScoresByUserId
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new
                {
                    userId = kv.Key,
                    username = nameByUserId.TryGetValue(kv.Key, out var u) ? u : kv.Key,
                    score = kv.Value
                })
                .ToArray();

            publicGameState = new
            {
                isActive = cs.IsActive,
                startedAtUtc = cs.StartedAtUtc,
                endedAtUtc = cs.EndedAtUtc,
                recentGuesses = cs.RecentGuesses,
                leaderboard
            };
        }

        // Return a controlled public shape so we always include players/hostUserId
        return new
        {
            roomId = state.RoomId,
            roomName = state.RoomName,
            activeGame = state.ActiveGame,
            gameState = publicGameState,
            updatedAtUtc = state.UpdatedAtUtc,

            hostUserId = state.HostUserId,
            players = players.Select(p => new
            {
                userId = p.UserId,
                name = p.Name,
                lastSeenUtc = p.LastSeenUtc,
                isConnected = p.IsConnected,
                isReady = p.IsReady,
                avatarUrl = p.AvatarUrl
            }).ToArray()
        };
    }
}
