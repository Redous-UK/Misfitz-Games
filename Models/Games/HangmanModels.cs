using Misfitz_Games.Models;

namespace Misfitz_Games.Models.Games;

public sealed record HangmanStartRequest(
    string? Word = null,
    int? MaxWrong = null
);

public sealed record HangmanGuessRequest(
    string Value
);

public sealed record HangmanState(
    string Word,
    HashSet<char> Guessed,
    List<string> WrongGuesses,
    int MaxWrong
);

