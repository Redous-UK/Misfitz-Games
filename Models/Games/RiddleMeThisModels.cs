using System.ComponentModel.DataAnnotations;

namespace Misfitz_Games.Models.Games;

public sealed record RiddleMeThisState(
    int Round,
    string RiddleId,
    string Category,
    string Riddle,
    string Answer,              // keep server-side; don’t expose in public view
    bool IsSolved,
    string? SolvedByUserId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SolvedAtUtc,
    List<RiddleGuess> RecentGuesses,
    List<string> UsedRiddleIds
);

public sealed record RiddleGuess(
    string UserId,
    string Guess,
    bool IsCorrect,
    DateTimeOffset AtUtc
);

public sealed record UsedRiddleIds(
long RiddleId,
string Category
);

public sealed class Riddle
{
    [Key]
    public long Id { get; set; }
    public string Category { get; set; } = "General";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Difficulty { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
};

public sealed class RiddleCatalog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(64)]
    public string Category { get; set; } = "General";
    [MaxLength(32)]
    public string Difficulty { get; set; } = "Easy"; // Easy/Medium/Hard
    [Required, MaxLength(2000)]
    public string Question { get; set; } = "";
    [Required, MaxLength(512)]
    public string Answer { get; set; } = ""; // canonical answer (normalized compare)
    [MaxLength(1024)]
    public string? AcceptableAnswersJson { get; set; } // JSON array of synonyms/alternates
    [MaxLength(1024)]
    public string? HintsJson { get; set; } // JSON array of hints
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public enum RiddleRoundStatus
{
    Idle = 0,
    Active = 1,
    Revealed = 2,
    Ended = 3
}

public sealed class RiddleRound
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; } // your room Guid
    [MaxLength(32)]
    public string RoomCode { get; set; } = ""; // optional (8-digit/custom code for quick query)
    public Guid? CatalogRiddleId { get; set; }
    public RiddleCatalog? CatalogRiddle { get; set; }
    [Required, MaxLength(2000)]
    public string Question { get; set; } = "";
    [Required, MaxLength(512)]
    public string Answer { get; set; } = ""; // stored for reveal; can copy from catalog
    [MaxLength(1024)]
    public string? HintsJson { get; set; }
    public RiddleRoundStatus Status { get; set; } = RiddleRoundStatus.Idle;
    public int RoundNumber { get; set; } = 1;
    public int BasePoints { get; set; } = 100;
    public int TimeLimitSeconds { get; set; } = 30;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? RevealAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    [MaxLength(64)]
    public string? WinnerUserId { get; set; } // whatever your user id type is
    [MaxLength(64)]
    public string? WinnerName { get; set; }
    public int? WinnerPoints { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RiddleSubmission
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public RiddleRound Round { get; set; } = default!;
    [MaxLength(64)]
    public string UserId { get; set; } = "";
    [MaxLength(64)]
    public string Username { get; set; } = "";
    [Required, MaxLength(512)]
    public string AnswerText { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

// Optional (fast leaderboard per room)
public sealed class RiddlePlayerStats
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    [MaxLength(64)]
    public string UserId { get; set; } = "";
    [MaxLength(64)]
    public string Username { get; set; } = "";
    public int TotalPoints { get; set; }
    public int CorrectCount { get; set; }
    public int PlayedCount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}