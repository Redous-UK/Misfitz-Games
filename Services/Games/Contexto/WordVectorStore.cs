namespace Misfitz_Games.Services.Contexto;

/// <summary>
/// Loads the Contexto vocabulary (and later vectors) from disk.
/// Never throws on missing/invalid vocab; will auto-create a starter vocab.
/// </summary>
public sealed class WordVectorStore
{
    private readonly Dictionary<string, float[]> _vectors;
    public IReadOnlyList<string> Vocabulary { get; }

    private static readonly string[] DefaultVocab =
    [
        "apple","banana","orange","coffee","pizza","bread","butter","cheese",
        "water","milk","tea","wine","beer","sugar","salt","pepper","honey",
        "chicken","beef","fish","rice","pasta","soup","burger","cake",
        "garden","forest","mountain","river","ocean","beach","island",
        "winter","summer","spring","autumn","weather","storm","snow",
        "sun","moon","star","planet","space","rocket","computer","internet",
        "music","movie","book","story","game","play","win","lose","score"
    ];

    public WordVectorStore(IWebHostEnvironment env, ILogger<WordVectorStore> log)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);

        var vocabPath = Path.Combine(dataDir, "contexto_vocab.txt");

        EnsureVocabFile(vocabPath, log);

        var vocab = LoadVocab(vocabPath, log);
        if (vocab.Count == 0)
        {
            log.LogWarning("Vocab file {Path} was empty. Re-seeding with defaults.", vocabPath);
            File.WriteAllLines(vocabPath, DefaultVocab);
            vocab = LoadVocab(vocabPath, log);
        }

        Vocabulary = vocab;

        // Placeholder vectors (semantic vectors plugged in later)
        _vectors = Vocabulary.ToDictionary(
            w => w,
            _ => new float[300],
            StringComparer.OrdinalIgnoreCase
        );

        log.LogInformation("Contexto vocabulary loaded: {Count} words from {Path}", Vocabulary.Count, vocabPath);
    }

    public bool TryGetVector(string word, out float[] vec)
        => _vectors.TryGetValue((word ?? string.Empty).Trim(), out vec!);

    private static void EnsureVocabFile(string vocabPath, ILogger log)
    {
        if (File.Exists(vocabPath))
            return;

        try
        {
            log.LogWarning("contexto_vocab.txt not found. Creating default vocab at {Path}", vocabPath);
            File.WriteAllLines(vocabPath, DefaultVocab);
        }
        catch (Exception ex)
        {
            // Worst-case fallback: keep running with in-memory defaults
            log.LogError(ex, "Failed to create vocab file at {Path}. App will run with in-memory defaults.", vocabPath);
        }
    }

    private static IReadOnlyList<string> LoadVocab(string vocabPath, ILogger log)
    {
        try
        {
            // Supports comments (#) and blank lines
            return File.ReadAllLines(vocabPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Where(l => !l.StartsWith('#'))
                .Select(l => l.ToLowerInvariant())
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to read vocab file {Path}. Falling back to defaults.", vocabPath);
            return DefaultVocab;
        }
    }
}
