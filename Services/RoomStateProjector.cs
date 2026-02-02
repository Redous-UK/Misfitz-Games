using System.Text.Json;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

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
        var players = state.Players ?? new List<PlayerPresence>();

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

        // If contexto, strip SecretWord from the public output
        object? publicGameState = state.GameState;

        if (cs is not null)
        {
            publicGameState = new
            {
                isActive = cs.IsActive,
                startedAtUtc = cs.StartedAtUtc,
                endedAtUtc = cs.EndedAtUtc,
                recentGuesses = cs.RecentGuesses,
                scoresByUserId = cs.ScoresByUserId
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