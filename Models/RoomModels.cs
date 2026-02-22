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

public sealed record RoomSummaryDto(
    Guid RoomId,
    string Name,
    string RoomCode,
    int PlayerCount,
    bool HasActiveGame,
    string? ActiveGame,
    DateTimeOffset CreatedAtUtc
);

public sealed record IdleRoomCandidate(
    Guid RoomId,
    string RoomCode,
    string Name,
    DateTimeOffset CreatedAtUtc,
    int PlayerCount,
    string? HostUserId,
    string ActiveGame,
    string Reason
);

public sealed record Room(
     Guid Id,
     string Code,
     long OwnerUserId,
     string Name,
     DateTime CreatedUtc,
     DateTime LastActiveUtc
)
{
    public AppUser Owner { get; init; } = null!;
};

public sealed record AppUser(
    long Id,
    string Username
);