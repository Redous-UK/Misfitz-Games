using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public sealed class ContextoEngine
{
    // MVP parsing: accept either "!guess word" or a single word message
    public bool TryExtractGuess(string message, out string guess)
    {
        guess = "";
        if (string.IsNullOrWhiteSpace(message)) return false;

        var m = message.Trim();
        if (m.StartsWith("!guess ", StringComparison.OrdinalIgnoreCase))
        {
            guess = m["!guess ".Length..].Trim();
            return guess.Length > 0;
        }

        // If it's a single token, treat it as a guess
        if (!m.Contains(' ') && !m.StartsWith('!'))
        {
            guess = m;
            return true;
        }

        return false;
    }

    public RoomState ApplyGuess(RoomState roomState, string userId, string username, string guess)
    {
        if (roomState.GameState is not ContextoState s || !s.IsActive)
            return roomState;

        var normalizedGuess = guess.Trim();
        if (normalizedGuess.Length == 0) return roomState;

        // MVP scoring:
        // - exact match wins
        // - otherwise score 0 (we’ll replace with embeddings later)
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
        var isActive = isWinner ? false : s.IsActive;

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

    public static ContextoState NewRound(string secretWord)
        => new(
            SecretWord: secretWord.Trim(),
            IsActive: true,
            StartedAtUtc: DateTimeOffset.UtcNow,
            EndedAtUtc: null,
            RecentGuesses: new List<ContextoGuess>(),
            ScoresByUserId: new Dictionary<string, int>()
        );
}