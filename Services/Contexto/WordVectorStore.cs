using Humanizer.Inflections;

namespace Misfitz_Games.Services.Contexto;

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

public WordVectorStore(
    IWebHostEnvironment env,
    ILogger<WordVectorStore> log
)
{
    var dataDir = Path.Combine(env.ContentRootPath, "Data");
    Directory.CreateDirectory(dataDir);

    var vocabPath = Path.Combine(dataDir, "contexto_vocab.txt");

    if (!File.Exists(vocabPath))
    {
        log.LogWarning(
            "contexto_vocab.txt not found. Creating default vocab at {Path}",
            vocabPath
        );

        File.WriteAllLines(vocabPath, DefaultVocab);
    }

    Vocabulary = [.. File.ReadAllLines(vocabPath)
        .Select(x => x.Trim().ToLowerInvariant())
        .Where(x => x.Length > 0)
        .Distinct()];

    // Placeholder vectors (semantic vectors plugged in later)
    _vectors = Vocabulary.ToDictionary(
        w => w,
        _ => new float[300]
    );

    log.LogInformation(
        "Contexto vocabulary loaded: {Count} words",
        Vocabulary.Count
    );
}

public bool TryGetVector(string word, out float[] vec)
    => _vectors.TryGetValue(
        (word ?? "").Trim().ToLowerInvariant(),
        out vec
    );
}