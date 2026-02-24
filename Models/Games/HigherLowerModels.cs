using Misfitz_Games.Models;

namespace Misfitz_Games.Models.Games;

public sealed class HigherLowerState
{
    public int Current { get; set; }
    public int Next { get; set; }

    public int Streak { get; set; }
    public int BestStreak { get; set; }

    // one answer per user per round
    public Dictionary<string, string> AnswersByUserId { get; set; } = [];

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
