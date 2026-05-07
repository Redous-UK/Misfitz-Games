namespace Misfitz_Games.Models.Battles.Requests;

public sealed record RequestBattleDto(
    string Title,
    string Description,
    string OpponentName,
    DateTimeOffset StartsAtUtc
);

public sealed record UpdateBattleStatusDto(
    string Status
);