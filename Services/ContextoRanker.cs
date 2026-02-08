namespace Misfitz_Games.Services;

public sealed class ContextoRankIndex(Dictionary<string, int> rankByWord, int maxRank)
{
    private readonly Dictionary<string, int> _rankByWord = rankByWord;
    public int MaxRank { get; } = maxRank;

    public int GetRank(string word)
        => _rankByWord.TryGetValue(word.ToLowerInvariant(), out var r)
            ? r
            : MaxRank;
}

public static class ContextoRanker
{
    public static ContextoRankIndex Build(
        string secretWord,
        WordVectorStore store,
        int maxRank = 10_000
    )
    {
        if (!store.TryGetVector(secretWord, out var secretVec))
            throw new InvalidOperationException("Secret word not in vocabulary.");

        var scored = new List<(string word, float sim)>();

        foreach (var w in store.Vocabulary)
        {
            if (!store.TryGetVector(w, out var v)) continue;
            var sim = Cosine(secretVec, v);
            scored.Add((w, sim));
        }

        scored.Sort((a, b) => b.sim.CompareTo(a.sim));

        var ranks = new Dictionary<string, int>();
        for (int i = 0; i < Math.Min(maxRank, scored.Count); i++)
            ranks[scored[i].word] = i + 1;

        return new ContextoRankIndex(ranks, maxRank);
    }

    public static int RankToPercentage(int rank, int maxRank)
    {
        if (rank <= 1) return 100;
        if (rank >= maxRank) return 0;

        return (int)Math.Round(
            100.0 * (1.0 - (rank - 1.0) / (maxRank - 1.0))
        );
    }

    private static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return dot / ((float)Math.Sqrt(na) * (float)Math.Sqrt(nb));
    }
}