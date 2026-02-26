using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.HigherLower;

public sealed class HigherLowerService
{
    private static readonly HigherLowerCard[] Deck =
    [
        new() { Label="2", Value=2 }, new() { Label="3", Value=3 }, new() { Label="4", Value=4 },
        new() { Label="5", Value=5 }, new() { Label="6", Value=6 }, new() { Label="7", Value=7 },
        new() { Label="8", Value=8 }, new() { Label="9", Value=9 }, new() { Label="10", Value=10 },
        new() { Label="J", Value=11 }, new() { Label="Q", Value=12 }, new() { Label="K", Value=13 },
        new() { Label="A", Value=14 },
    ];

    private readonly Random _rng = new();

    private HigherLowerCard Draw() => Deck[_rng.Next(Deck.Length)];

    public HigherLowerState NewGame()
    {
        return new HigherLowerState
        {
            Status = HigherLowerStatus.InRound,
            Current = Draw(),
            RevealedNext = null,
            Streak = 0,
            BestStreak = 0,
            LastChoice = null,
            LastWasCorrect = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void GuessInPlace(HigherLowerState s, string choice)
    {
        choice = (choice ?? "").Trim().ToLowerInvariant();
        if (choice is not ("higher" or "lower"))
            throw new ArgumentException("choice must be 'higher' or 'lower'");

        if (s.Status != HigherLowerStatus.InRound)
            throw new InvalidOperationException("Round not active");

        var next = Draw();

        // tie = loss (change if you want different rule)
        var correct = choice == "higher"
            ? next.Value > s.Current.Value
            : next.Value < s.Current.Value;

        s.LastChoice = choice;
        s.LastWasCorrect = correct;

        if (correct)
        {
            s.Streak++;
            if (s.Streak > s.BestStreak) s.BestStreak = s.Streak;

            // advance
            s.Current = next;
            s.RevealedNext = null;
            s.Status = HigherLowerStatus.InRound;
        }
        else
        {
            s.RevealedNext = next;
            s.Status = HigherLowerStatus.Revealed;
        }

        s.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ContinueAfterLossInPlace(HigherLowerState s)
    {
        s.Streak = 0;
        s.LastChoice = null;
        s.LastWasCorrect = null;

        s.Current = Draw();
        s.RevealedNext = null;
        s.Status = HigherLowerStatus.InRound;
        s.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}