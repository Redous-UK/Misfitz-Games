namespace Misfitz_Games.Services;

public sealed class ContextoWordProvider
{
    private static readonly string[] Words =
    [
        "apple", "banana", "orange", "coffee", "pizza", "rocket", "dragon", "winter", "garden", "ocean"
    ];

    private readonly Random _rng = new();

    public string NextSecret()
        => Words[_rng.Next(Words.Length)];
}