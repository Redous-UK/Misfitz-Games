using Misfitz_Games.Models;

namespace Misfitz_Games.Models.Games;

public enum HigherLowerStatus
{
    Idle = 0,
    InRound = 1,
    Revealed = 2,
    Finished = 3
}

public sealed class HigherLowerCard
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
}

public sealed class HigherLowerState
{
    public HigherLowerStatus Status { get; set; } = HigherLowerStatus.Idle;

    public HigherLowerCard Current { get; set; } = new();
    public HigherLowerCard? RevealedNext { get; set; } // only for “you lost” reveal moment

    public int Streak { get; set; }
    public int BestStreak { get; set; }

    public string? LastChoice { get; set; } // "higher" | "lower"
    public bool? LastWasCorrect { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
