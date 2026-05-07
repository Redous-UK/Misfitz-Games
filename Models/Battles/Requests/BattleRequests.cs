namespace Misfitz_Games.Models.Battles.Requests;

public sealed record RequestBattleDto(
    string? Title,
    string? OpponentName,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc
);

public sealed record UpdateBattleStatusDto(string? Status);

public sealed record UpdateBattleDto(
    string? Title,
    string? RoomRef,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc
);