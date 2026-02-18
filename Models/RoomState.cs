namespace Misfitz_Games.Models;

public enum GameType
{
    None = 0,
    Contexto = 1,
    Deal = 2,
    Hangman = 3,
    Trivia = 4
}

public sealed record RoomState(
    Guid RoomId,
    string RoomName,
    GameType ActiveGame,
    object? GameState,
    DateTimeOffset UpdatedAtUtc,
    List<PlayerPresence>? Players = null,
    string? HostUserId = null
);
