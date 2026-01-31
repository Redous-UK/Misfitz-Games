using System.Text.Json;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public sealed class ContextoEngine
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RoomState ApplyGuess(RoomState roomState, string userId, string username, string guess)
    {
        var s = GetContextoState(roomState);

        // 🔎 Optional: quick debug breadcrumb (remove later)
        // Console.WriteLine($"ApplyGuess: activeGame={roomState.ActiveGame} gameStateType={roomState.GameState?.GetType().Name} isActive={(s?.IsActive.ToString() ?? "null")}");

        if (s is null || !s.IsActive)
            return roomState;

        var normalizedGuess = guess.Trim();
        if (normalizedGuess.Length == 0)
            return roomState;

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

    private static ContextoState? GetContextoState(RoomState roomState)
    {
        if (roomState.GameState is null) return null;

        if (roomState.GameState is ContextoState cs) return cs;

        if (roomState.GameState is JsonElement je)
        {
            try { return je.Deserialize<ContextoState>(JsonOpts); }
            catch { return null; }
        }

        return null;
    }

    // Keep this if your controllers call ContextoEngine.NewRound(...)
    public static ContextoState NewRound(string secretWord)
    {
        var normalized = (secretWord ?? "").Trim();
        if (normalized.Length == 0)
            throw new ArgumentException("Secret word is required", nameof(secretWord));

        return new ContextoState(
            SecretWord: normalized,
            IsActive: true,
            StartedAtUtc: DateTimeOffset.UtcNow,
            EndedAtUtc: null,
            RecentGuesses: new List<ContextoGuess>(),
            ScoresByUserId: new Dictionary<string, int>()
        );
    }

    public bool TryExtractGuess(string message, out string guess)
    {
        guess = string.Empty;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = message.Trim();

        // Accept "!guess apple"
        if (text.StartsWith("!guess ", StringComparison.OrdinalIgnoreCase))
        {
            guess = text[7..].Trim();
            return guess.Length > 0;
        }

        // Accept single-word guesses like "apple"
        // (TikTok connector already normalizes these, but admin UI might not)
        if (IsSingleWord(text))
        {
            guess = text;
            return true;
        }

        return false;
    }

    private static bool IsSingleWord(string text)
    {
        if (text.Length < 2 || text.Length > 32)
            return false;

        // letters/numbers only (unicode safe)
        return text.All(char.IsLetterOrDigit);
    }
}