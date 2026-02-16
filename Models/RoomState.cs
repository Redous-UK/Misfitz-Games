namespace Misfitz_Games.Models;

public enum GameType
{
    None = 0,
    Contexto = 1,
    Deal = 2,
    Hangman = 3
}

// High-level room info
public sealed record RoomState(
    Guid RoomId,
    string RoomName,
    GameType ActiveGame,
    object? GameState,
    DateTimeOffset UpdatedAtUtc,
    List<PlayerPresence>? Players = null,
    string? HostUserId = null
);

// Players / presence




//Hangman Specific
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

//Contexto Specific
public sealed record ContextoStartRequest(string SecretWord);

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

public sealed record GuessResponse(
    bool Complete,
    bool Incorrect,
    int Percentage,
    string Guess
);
