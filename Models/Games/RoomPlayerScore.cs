using System.ComponentModel.DataAnnotations;

namespace Misfitz_Games.Models;

public sealed class RoomPlayerScore
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoomId { get; set; }

    [MaxLength(64)]
    public string UserId { get; set; } = "";

    [MaxLength(64)]
    public string Username { get; set; } = "";

    public int TotalScore { get; set; }

    public int TriviaScore { get; set; }
    public int HangmanScore { get; set; }
    public int HigherLowerScore { get; set; }
    public int RiddleScore { get; set; }
    public int ContextoScore { get; set; }
    public int DealScore { get; set; }

    public int GamesPlayed { get; set; }
    public int Wins { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}