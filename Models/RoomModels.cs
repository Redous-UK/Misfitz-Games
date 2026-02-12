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
    int RoomId,
    string Name,
    string RoomCode,
    int PlayerCount,
    bool HasActiveGame,
    string? ActiveGame,
    DateTimeOffset CreatedAtUtc
)
{
    public RoomSummaryDto(Guid RoomId, string Name, string RoomCode, DateTimeOffset CreatedAtUtc, int PlayerCount, bool HasActiveGame, string? ActiveGame)
    {
        this.Name = Name;
        this.RoomCode = RoomCode;
        this.CreatedAtUtc = CreatedAtUtc;
        this.PlayerCount = PlayerCount;
        this.HasActiveGame = HasActiveGame;
        this.ActiveGame = ActiveGame;
    }
}
