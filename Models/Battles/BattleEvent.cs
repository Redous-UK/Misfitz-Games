namespace Misfitz_Games.Models.Battles;

public class BattleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RequestedByUserId { get; set; }
    
    public Guid OwnerUserId { get; set; }

    public string Title { get; set; } = "";
    public string? RoomRef { get; set; }
    public string? Description { get; set; } = "";
    public string? OpponentName { get; set; } = "";

    public string Status { get; set; } = "pending";
    // pending, approved, declined, completed

    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }