using Misfitz_Games.Models;

public sealed record LeaderboardUpdate(
    Guid RoomId,
    GameType GameType,
    IReadOnlyDictionary<string, int> ScoresByUserId,
    IReadOnlyDictionary<string, string> UsernamesByUserId,
    string? WinnerUserId,
    string RoundKey
);

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