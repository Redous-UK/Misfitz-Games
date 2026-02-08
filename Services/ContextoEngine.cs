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

    // Treat rank as: 1 = best/closest, maxRank = worst
    public static int RankToPercentage(int rank, int maxRank)
    {
        if (rank <= 1) return 100;
        if (rank >= maxRank) return 0;

        var pct = 100 * (1.0 - (rank - 1.0) / (maxRank - 1.0));
        return (int)Math.Round(pct);
    }

    // Until you have a real global dictionary rank, we can convert a closeness score into a pseudo-rank.
    // Score is expected: 0..maxScore where higher = closer.
    public static int ScoreToRank(int score, int maxScore)
    {
        // Higher score = better rank (1 is best)
        if (score <= 0) return maxScore;
        if (score >= maxScore) return 1;
        return Math.Max(1, maxScore - score + 1);
    }

    /// <summary>
    /// Temporary, *non-Levenshtein* closeness scorer to unlock Percentage today.
    /// Uses Dice coefficient over character bigrams (0..1). This is NOT semantic like real Contexto,
    /// but it's stable and gives players a "warmth" signal.
    ///
    /// Later, replace this with semantic similarity (embeddings) WITHOUT changing any controller/UI contracts.
    /// </summary>
    private static double DiceBigramSimilarity(string a, string b)
    {
        a = (a ?? string.Empty).Trim().ToLowerInvariant();
        b = (b ?? string.Empty).Trim().ToLowerInvariant();

        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length < 2 || b.Length < 2) return a == b ? 1.0 : 0.0;

        static Dictionary<string, int> Bigrams(string s)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < s.Length - 1; i++)
            {
                var bg = s.Substring(i, 2);
                map[bg] = map.TryGetValue(bg, out var cur) ? cur + 1 : 1;
            }
            return map;
        }

        var aB = Bigrams(a);
        var bB = Bigrams(b);

        int overlap = 0;
        int aCount = 0;
        int bCount = 0;

        foreach (var kv in aB) aCount += kv.Value;
        foreach (var kv in bB) bCount += kv.Value;

        foreach (var kv in aB)
        {
            if (bB.TryGetValue(kv.Key, out var bN))
                overlap += Math.Min(kv.Value, bN);
        }

        // Dice coefficient
        return (2.0 * overlap) / (aCount + bCount);
    }

    public RoomState ApplyGuess(RoomState roomState, string userId, string username, string guess)
    {
        var s = GetContextoState(roomState);
        if (s is null || !s.IsActive)
            return roomState;

        var normalizedGuess = (guess ?? string.Empty).Trim();
        if (normalizedGuess.Length == 0)
            return roomState;

        const int maxRank = 10000; // "ranking universe" size for percentage curve

        var isWinner = string.Equals(normalizedGuess, s.SecretWord, StringComparison.OrdinalIgnoreCase);

        // ---
        // 1) Compute a closeness score 0..maxRank (higher = closer)
        //    Winner should be maxRank so they get 100%.
        // ---
        int closenessScore;
        if (isWinner)
        {
            closenessScore = maxRank;
        }
        else
        {
            // TEMP: non-Levenshtein closeness to unlock % today.
            // Swap this out later for semantic similarity (embeddings).
            var sim01 = DiceBigramSimilarity(normalizedGuess, s.SecretWord); // 0..1
            closenessScore = (int)Math.Round(sim01 * maxRank);

            // Keep 100% exclusive to exact match (optional, but feels right)
            if (closenessScore >= maxRank) closenessScore = maxRank - 1;
        }

        // 2) Convert to pseudo-rank and percentage using your existing helpers
        int pseudoRank = ScoreToRank(closenessScore, maxRank);
        int percent = RankToPercentage(pseudoRank, maxRank);

        // 3) Update points (your ScoresByUserId is "simple points")
        //    Keep points separate from closeness, so the scoreboard isn't polluted.
        var newScores = new Dictionary<string, int>(s.ScoresByUserId);
        if (isWinner)
            newScores[userId] = newScores.TryGetValue(userId, out var cur) ? cur + 1 : 1;

        // 4) Add guess entry
        //    Store pseudoRank in RankOrScore for now (because you don't have true rank yet).
        var newGuess = new ContextoGuess(
            UserId: userId,
            Username: username,
            Guess: normalizedGuess,
            Percentage: percent,
            RankOrScore: pseudoRank,
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
