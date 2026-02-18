namespace Misfitz_Games.Models.Games;

public sealed record TriviaQuestion(
    string Category,
    string Difficulty,
    string Question,
    string CorrectAnswer,
    List<string> IncorrectAnswers,
    List<string> ShuffledAnswers
);

public sealed record TriviaRoundState(
    bool Active,
    TriviaQuestion? Current,
    DateTimeOffset? AskedAtUtc,
    bool Revealed,
    Dictionary<string, int> ScoresByUserId,
    HashSet<string> AnsweredThisQuestionUserIds,

    // Timers / automation (defaults keep call-sites stable)
    DateTimeOffset? EndsAtUtc = null,
    bool AutoNext = false,
    int AutoNextDelaySeconds = 7,
    DateTimeOffset? NextStartsAtUtc = null,
    int RoundSeconds = 20
);