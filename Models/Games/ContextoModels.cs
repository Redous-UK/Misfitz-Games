namespace Misfitz_Games.Models.Games;

public sealed record ContextoState(
    string SecretWord,
    bool IsActive,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    List<ContextoGuess> RecentGuesses,
    Dictionary<string, int> ScoresByUserId // simple points
);

public sealed record ContextoGuess(
    string UserId,
    string Username,
    string Guess,
    int Percentage,
    int RankOrScore,
    bool IsWinner,
    DateTimeOffset TsUtc
);

//public sealed record GuessResponse(
//    bool Complete,
//    bool Incorrect,
//    int Percentage,
//    string Guess
//);

