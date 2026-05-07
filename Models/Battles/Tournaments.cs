namespace Misfitz_Games.Models.Battles;

public sealed class Tournament
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
    public string Title { get; set; } = "";
    public string Game { get; set; } = "";
    public int RequiredSignups { get; set; }

    public string? Prize { get; set; }
    public string? Description { get; set; }

    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Status { get; set; } = "draft";
}

public sealed class TournamentSignup
{
    public Guid Id { get; set; }

    public Guid TournamentId { get; set; }
    public Guid UserId { get; set; }

    public DateTimeOffset SignedUpAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Tournament Tournament { get; set; } = null!;
}

public sealed record CreateTournamentDto(
    string? Title,
    string Name,
    string Game,
    int RequiredSignups,
    string? Prize,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Status
);

public sealed record UpdateTournamentDto(
    string? Title,
    int RequiredSignups,
    string? Prize,
    string? Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Status
);