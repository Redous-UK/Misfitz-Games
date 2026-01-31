using System.Text.Json;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public sealed class ContextoEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RoomState ApplyGuess(RoomState roomState, string userId, string username, string guess)
    {
        var s = GetState(roomState);
        if (s is null || !s.IsActive)
            return roomState;

        var normalizedGuess = guess.Trim();
        if (normalizedGuess.Length == 0) return roomState;

        var isWinner = string.Equals(normalizedGuess, s.SecretWord, StringComparison.OrdinalIgnoreCase);
        var score = isWinner ? 1 : 0;

        var newScores = new Dictionary<string, int>(s.ScoresByUserId);
        if (score > 0)
            newScores[userId] = newScores.TryGetValue(userId, out var cur) ? cur + score : score;

        var newGuess = new ContextoGuess(
            UserId: userId,
            Username: username,
            Guess: normalizedGuess,
            RankOrScore: isWinner ? 1 : 0,
            IsWinner: isWinner,
            TsUtc: DateTimeOffset.UtcNow
        );

        var guesses = new List<ContextoGuess>(s.RecentGuesses);
        guesses.Insert(0, newGuess);
        if (guesses.Count > 30) guesses.RemoveRange(30, guesses.Count - 30);

        var endedAt = isWinner ? DateTimeOffset.UtcNow : s.EndedAtUtc;
        var isActive = !isWinner && s.IsActive;

        var next = s with
        {
            RecentGuesses = guesses,
            ScoresByUserId = newScores,
            IsActive = isActive,
            EndedAtUtc = endedAt
        };

        return roomState with
        {
            GameState = next,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static ContextoState? GetState(RoomState roomState)
    {
        if (roomState.GameState is null) return null;

        // Works when state is still in-memory as the proper type
        if (roomState.GameState is ContextoState cs) return cs;

        // Works after Redis/JSON round-trip (GameState becomes JsonElement)
        if (roomState.GameState is JsonElement je)
        {
            try
            {
                return je.Deserialize<ContextoState>(JsonOpts);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}