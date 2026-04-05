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

public sealed class Room
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime LastActiveUtc { get; set; }

    public string DefaultGame { get; set; } = "None";
    public bool AutoRestore { get; set; } = true;
    public bool AllowGuests { get; set; } = true;
    public bool OverlaysEnabled { get; set; } = true;
    public bool IsPrivate { get; set; } = false;
}

public sealed class AppUser
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
}

public sealed record LeaderboardEntryDto(
    string UserId,
    string Username,
    double Score,
    int Wins,
    DateTimeOffset? UpdatedAtUtc
);

public sealed record LeaderboardPlayerStatsDto(
    string UserId,
    string Username,
    int Wins,
    DateTimeOffset? UpdatedAtUtc
);