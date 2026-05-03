namespace Misfitz_Games.Models.Battles;

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";
    public string Game { get; set; } = "";

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}