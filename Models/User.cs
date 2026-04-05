namespace Misfitz_Games.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    public byte[] PasswordHash { get; set; } = [];
    public byte[] PasswordSalt { get; set; } = [];
    public string Role { get; set; } = "member";

    // Portal / profile
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsProfilePublic { get; set; } = true;
    public bool ShowAvatarInRoom { get; set; } = true;
    public bool ShowOnlineStatus { get; set; } = false;
    
    // Notifications / preferences
    public bool EmailAlerts { get; set; } = true;
    public bool SecurityAlerts { get; set; } = true;
    public bool GameReminders { get; set; } = false;

    public string DigestFrequency { get; set; } = "Weekly";
    public string Timezone { get; set; } = "Europe/London";
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Misfitz";

    public bool CompactLayout { get; set; } = false;
    public bool ShowTips { get; set; } = true;
    public bool PublicRoomListing { get; set; } = true;
    public bool ShowGameplayStats { get; set; } = true;

    public string? HomeRoomCode { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginUtc { get; set; }
}

public sealed class UserIdMap
{
    public long Id { get; set; }                // numeric surrogate key
    public string UserGuid { get; set; } = "";  // the claim value (GUID string)
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}