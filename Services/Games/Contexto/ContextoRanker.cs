namespace Misfitz_Games.Services.Contexto;

public sealed class ContextoRankIndex
{
    private readonly Dictionary<string, int> _rankByWord;

    public string SecretWord { get; }
    public int MaxRank { get; }

    public ContextoRankIndex(string secretWord, Dictionary<string, int> rankByWord, int maxRank)
    {
        SecretWord = secretWord;
        _rankByWord = rankByWord;
        MaxRank = maxRank;
    }

    public int GetRank(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return MaxRank;

        return _rankByWord.TryGetValue(word.Trim().ToLowerInvariant(), out var r)
            ? r
            : MaxRank;
    }

    /// <summary>
    /// Safety fallback index: only the secret word is rank #1.
    /// Everything else returns MaxRank.
    /// </summary>
    public static ContextoRankIndex FallbackOnlySecret(string secretWord, int maxRank)
    {
        var s = (secretWord ?? string.Empty).Trim().ToLowerInvariant();

        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (s.Length > 0)
            ranks[s] = 1;

        return new ContextoRankIndex(s, ranks, maxRank);
    }
}

public static class ContextoRanker
{
    public static ContextoRankIndex Build(
        string secretWord,
        WordVectorStore store,
        int maxRank = 10_000
    )
    {
        var secret = (secretWord ?? string.Empty).Trim().ToLowerInvariant();

        // If secret isn't in vocab/vectors yet, don't crash the app.
        if (!store.TryGetVector(secret, out var secretVec))
            return ContextoRankIndex.FallbackOnlySecret(secret, maxRank);

        var scored = new List<(string word, float sim)>(store.Vocabulary.Count);

        foreach (var wRaw in store.Vocabulary)
        {
            var w = (wRaw ?? string.Empty).Trim().ToLowerInvariant();
            if (w.Length == 0) continue;

            if (!store.TryGetVector(w, out var v)) continue;

            var sim = Cosine(secretVec, v);
            scored.Add((w, sim));
        }

        // Sort best-to-worst (highest cosine similarity first)
        scored.Sort((a, b) => b.sim.CompareTo(a.sim));

        // Assign ranks: 1..maxRank
        var ranksDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var limit = Math.Min(maxRank, scored.Count);
        for (int i = 0; i < limit; i++)
            ranksDict[scored[i].word] = i + 1;

        // Ensure the secret itself is rank 1
        // (should already be true, but keep it robust)
        ranksDict[secret] = 1;

        return new ContextoRankIndex(secret, ranksDict, maxRank);
    }

    /// <summary>
    /// Contexto-style mapping: rank 1 => 100%, rank maxRank => 0%
    /// </summary>
    public static int RankToPercentage(int rank, int maxRank)
    {
        if (rank <= 1) return 100;
        if (rank >= maxRank) return 0;

        var pct = 100.0 * (1.0 - (rank - 1.0) / (maxRank - 1.0));
        return (int)Math.Round(pct);
    }

    private static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        // If vectors are mismatched, bail safely.
        var len = Math.Min(a.Length, b.Length);
        if (len == 0) return 0;

        float dot = 0, na = 0, nb = 0;
        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na <= 0 || nb <= 0) return 0;

        return dot / ((float)Math.Sqrt(na) * (float)Math.Sqrt(nb));
    }
}
