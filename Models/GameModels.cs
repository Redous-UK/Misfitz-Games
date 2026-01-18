namespace Misfitz_Games.Models;

public enum GameType
{
    None = 0,
    Contexto = 1,
    Deal = 2
}

public sealed record RoomState(
    Guid RoomId,
    string RoomName,
    GameType ActiveGame,
    object? GameState,
    DateTimeOffset UpdatedAtUtc
);

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
    int RankOrScore,
    bool IsWinner,
    DateTimeOffset TsUtc
);