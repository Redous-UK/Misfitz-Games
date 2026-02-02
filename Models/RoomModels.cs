namespace Misfitz_Games.Models;

public sealed record RoomCreateRequest(
    string Name,
    string? RoomCode = null
);

public sealed record RoomDto(
    Guid RoomId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    string RoomCode
);

public sealed record RoomStatsDto(
    Guid RoomId,
    long GamesPlayed,
    long GuessesTotal,
    DateTimeOffset? LastActivityUtc
);

public sealed record PlayerPresence(
    string UserId,
    string Name,
    DateTimeOffset LastSeenUtc,
    bool IsConnected,
    bool IsReady = false,
    string? AvatarUrl = null
);

public sealed record RoomState(
    Guid RoomId,
    string RoomName,
    GameType ActiveGame,
    object? GameState,
    DateTimeOffset UpdatedAtUtc,
    List<PlayerPresence>? Players = null,
    string? HostUserId = null
);