namespace Misfitz_Games.Models.Games;

public sealed record RiddleMeThisState(
    int Round,
    string Riddle,
    string Answer,              // keep server-side; don’t expose in public view
    bool IsSolved,
    string? SolvedByUserId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SolvedAtUtc,
    List<RiddleGuess> RecentGuesses
);

public sealed record RiddleGuess(
    string UserId,
    string Guess,
    bool IsCorrect,
    DateTimeOffset AtUtc
);