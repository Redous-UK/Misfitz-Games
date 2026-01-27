namespace Misfitz_Games.Services;

public static class RoomCodeGenerator
{
    private static readonly Random Rng = new();

    public static string NewCode() => Rng.Next(0, 100_000_000).ToString("D8");
}