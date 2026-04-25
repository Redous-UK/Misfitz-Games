namespace Misfitz_Games.Models;

public enum GameType
{
    None = 0,
    Contexto = 1,
    Deal = 2,
    Hangman = 3,
    Trivia = 4,
    HigherLower = 5,
    RiddleMeThis = 6
}

public sealed record RoomState(
    Guid RoomId,
    string RoomName,
    string RoomCode,
    GameType ActiveGame,
    object? GameState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    List<PlayerPresence>? Players = null,
    string? HostUserId = null
);
