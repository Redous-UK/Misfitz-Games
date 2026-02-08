namespace Misfitz_Games.Services.Contexto;

public sealed class WordVectorStore
{
    private readonly Dictionary<string, float[]> _vectors;
    public IReadOnlyList<string> Vocabulary { get; }

    public WordVectorStore(IWebHostEnvironment env)
    {
        var dataPath = Path.Combine(env.ContentRootPath, "Data");

        var vocabPath = Path.Combine(dataPath, "contexto_vocab.txt");
        var vecPath = Path.Combine(dataPath, "contexto_vectors.bin");

        Vocabulary = [.. File.ReadAllLines(vocabPath)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)];

        // TEMP: stub vectors (until you drop real embeddings in)
        _vectors = Vocabulary.ToDictionary(
            w => w,
            _ => new float[300] // placeholder vector size
        );
    }

    public bool TryGetVector(string word, out float[] vec)
    {
        return _vectors.TryGetValue(word.ToLowerInvariant(), out vec);
    }
}