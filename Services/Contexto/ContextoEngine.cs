using System.Text.Json;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services.Contexto;

public sealed class ContextoEngine(WordVectorStore vectors, ContextoRankIndexStore rankStore)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WordVectorStore _vectors = vectors;
    private readonly ContextoRankIndexStore _rankStore = rankStore;

    // Classic Contexto: 1 is closest, 10_000 is furthest
    private const int MaxRank = 10_000;

    /// <summary>
    /// Rank -> Percentage mapping (Contexto-style).
    /// rank=1 => 100%, rank=maxRank => 0%
    /// </summary>
    public static int RankToPercentage(int rank, int maxRank)
    {
        if (rank <= 1) return 100;
        if (rank >= maxRank) return 0;

        var pct = 100.0 * (1.0 - (rank - 1.0) / (maxRank - 1.0));
        return (int)Math.Round(pct);
    }

    /// <summary>
    /// Ensures a rank index exists for this room/secret word.
    /// Builds it lazily if missing.
    /// </summary>
    private ContextoRankIndex EnsureRankIndex(Guid roomId, string secretWord)
    {
        if (_rankStore.TryGet(roomId, out var existing))
            return existing;

        // Build rank table for this secret (semantic ranking)
        var idx = ContextoRanker.Build(secretWord, _vectors, MaxRank);
        _rankStore.Set(roomId, idx);
        return idx;
    }

    /// <summary>
    /// Apply a guess to RoomState. Uses semantic rank (1..10000) and percentage derived from rank.
    /// </summary>
    public RoomState ApplyGuess(RoomState roomState, string userId, string username, string guess)
    {
        var s = GetContextoState(roomState);
        if (s is null || !s.IsActive)
            return roomState;

        var normalizedGuess = (guess ?? string.Empty).Trim();
        if (normalizedGuess.Length == 0)
            return roomState;

        // Ensure rank index exists for this room's current secret
        var index = EnsureRankIndex(roomState.RoomId, s.SecretWord);

        // Rank is semantic closeness ordering (1 best, MaxRank worst)
        var rank = index.GetRank(normalizedGuess);

        // Winner if it's rank 1 (secret should be rank 1) OR exact match as a safety check
        var isWinner =
            rank == 1 ||
            string.Equals(normalizedGuess, s.SecretWord, StringComparison.OrdinalIgnoreCase);

        // Percentage derived from rank
        var percent = isWinner ? 100 : RankToPercentage(rank, index.MaxRank);

        // NOTE: Your ContextoState currently uses ScoresByUserId as "simple points".
        // We’ll keep your existing behavior: only increment on win (same as your current engine).
        var newScores = new Dictionary<string, int>(s.ScoresByUserId);
        if (isWinner)
            newScores[userId] = newScores.TryGetValue(userId, out var cur) ? cur + 1 : 1;

        // Persist guess (RankOrScore is now used as Rank)
        var newGuess = new ContextoGuess(
            UserId: userId,
            Username: username,
            Guess: normalizedGuess,
            Percentage: percent,
            RankOrScore: rank, // <-- this is now the semantic rank (1..10000)
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
    // NOTE: This only creates the state. The semantic rank index is built lazily
    // on first guess via EnsureRankIndex(roomId, secretWord).
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
            RecentGuesses: [],
            ScoresByUserId: []
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
