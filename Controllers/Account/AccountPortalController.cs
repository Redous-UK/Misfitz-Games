using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Account;
using static Misfitz_Games.Models.Account.AccountModels;

namespace Misfitz_Games.Controllers.Account;

[ApiController]
[Authorize]
public class AccountPortalController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;

    [HttpGet("/account/me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "guest";
        var name = User.FindFirstValue(ClaimTypes.Name) ?? "";

        return Ok(new
        {
            userId,
            email,
            role,
            name
        });
    }

    [HttpGet("/account/portal")]
    public async Task<IActionResult> GetPortal(CancellationToken ct)
    {
        var userGuidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userGuidValue))
            return Unauthorized(new { error = "Invalid user id." });

        var map = await _db.UserIdMaps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserGuid == userGuidValue, ct);

        if (map is null)
            return NotFound(new { error = "No user mapping found." });

        if (!Guid.TryParse(map.UserGuid, out var userGuid))
            return BadRequest(new { error = "Mapped user guid is invalid." });

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userGuid, ct);

        if (user is null)
            return NotFound(new { error = "No user is found." });

        // Guests can access the portal, but they should never auto-own or auto-load a room.
        if (string.Equals(user.Role, "guest", StringComparison.OrdinalIgnoreCase))
        {
            var guestDto = new PortalStateDto(
                User: new PortalUserDto(
                    UserId: user.Id.ToString(),
                    Email: user.Email,
                    DisplayName: user.DisplayName ?? user.Username,
                    Username: user.Username,
                    Bio: user.Bio,
                    AvatarUrl: user.AvatarUrl,
                    IsProfilePublic: user.IsProfilePublic,
                    ShowAvatarInRoom: user.ShowAvatarInRoom,
                    ShowOnlineStatus: user.ShowOnlineStatus,
                    Role: user.Role
                ),
                Room: null,
                Preferences: new PortalPreferencesDto(
                    EmailAlerts: user.EmailAlerts,
                    SecurityAlerts: user.SecurityAlerts,
                    GameReminders: user.GameReminders,
                    DigestFrequency: user.DigestFrequency,
                    Timezone: user.Timezone,
                    Theme: user.Theme,
                    Accent: user.Accent,
                    CompactLayout: user.CompactLayout,
                    ShowTips: user.ShowTips,
                    PublicRoomListing: user.PublicRoomListing,
                    ShowGameplayStats: user.ShowGameplayStats
                )
            );

            return Ok(guestDto);
        }

        // Only use the current active room. If historical duplicates exist, prefer the most recent active one.
        var room = await _db.Rooms
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userGuid && x.IsActive)
            .OrderByDescending(x => x.LastActiveUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        var dto = new PortalStateDto(
            User: new PortalUserDto(
                UserId: user.Id.ToString(),
                Email: user.Email,
                DisplayName: user.DisplayName ?? user.Username,
                Username: user.Username,
                Bio: user.Bio,
                AvatarUrl: user.AvatarUrl,
                IsProfilePublic: user.IsProfilePublic,
                ShowAvatarInRoom: user.ShowAvatarInRoom,
                ShowOnlineStatus: user.ShowOnlineStatus,
                Role: user.Role
            ),
            Room: room is null
                ? null
                : new PortalRoomDto(
                    RoomId: room.Id.ToString(),
                    RoomName: room.Name,
                    RoomCode: room.Code,
                    Description: room.Description,
                    DefaultGame: room.DefaultGame,
                    AutoRestore: room.AutoRestore,
                    AllowGuests: room.AllowGuests,
                    OverlaysEnabled: room.OverlaysEnabled,
                    IsPrivate: room.IsPrivate,
                    IsActive: room.IsActive,
                    PortalPath: $"/play.html?roomRef={room.Code}"
                ),
            Preferences: new PortalPreferencesDto(
                EmailAlerts: user.EmailAlerts,
                SecurityAlerts: user.SecurityAlerts,
                GameReminders: user.GameReminders,
                DigestFrequency: user.DigestFrequency,
                Timezone: user.Timezone,
                Theme: user.Theme,
                Accent: user.Accent,
                CompactLayout: user.CompactLayout,
                ShowTips: user.ShowTips,
                PublicRoomListing: user.PublicRoomListing,
                ShowGameplayStats: user.ShowGameplayStats
            )
        );

        return Ok(dto);
    }

    [HttpPost("/account/portal/profile")]
    public async Task<IActionResult> SaveProfile([FromBody] SavePortalProfileRequest req, CancellationToken ct)
    {
        var userGuidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userGuidValue))
            return Unauthorized(new { error = "Not signed in." });

        if (!Guid.TryParse(userGuidValue, out var userGuid))
            return Unauthorized(new { error = "Invalid user id." });

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userGuid, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        user.DisplayName = req.DisplayName?.Trim() ?? "";
        user.Email = req.Email.Trim();
        user.Username = req.Username?.Trim() ?? "";
        user.Bio = req.Bio?.Trim();
        user.AvatarUrl = req.AvatarUrl?.Trim();
        user.IsProfilePublic = req.IsProfilePublic;
        user.ShowAvatarInRoom = req.ShowAvatarInRoom;
        user.ShowOnlineStatus = req.ShowOnlineStatus;

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("/account/portal/room")]
    public async Task<IActionResult> SaveRoom([FromBody] SavePortalRoomRequest req, CancellationToken ct)
    {
        var userGuidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userGuidValue))
            return Unauthorized(new { error = "Not signed in." });

        if (!Guid.TryParse(userGuidValue, out var userGuid))
            return Unauthorized(new { error = "Invalid user id." });

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userGuid, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        if (string.Equals(user.Role, "guest", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var room = await _db.Rooms
            .Where(x => x.OwnerUserId == userGuid && x.IsActive)
            .OrderByDescending(x => x.LastActiveUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        if (room is null)
            return NotFound(new { error = "Active room not found." });

        room.Name = req.RoomName?.Trim() ?? "";
        room.Description = req.Description?.Trim();
        room.DefaultGame = req.DefaultGame?.Trim() ?? "None";
        room.AutoRestore = req.AutoRestore;
        room.AllowGuests = req.AllowGuests;
        room.OverlaysEnabled = req.OverlaysEnabled;
        room.IsPrivate = req.IsPrivate;
        room.IsActive = req.IsActive;
        room.LastActiveUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("/account/portal/preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] SavePortalPreferencesRequest req, CancellationToken ct)
    {
        var userGuidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userGuidValue))
            return Unauthorized(new { error = "Not signed in." });

        if (!Guid.TryParse(userGuidValue, out var userGuid))
            return Unauthorized(new { error = "Invalid user id." });

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userGuid, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        user.EmailAlerts = req.EmailAlerts;
        user.SecurityAlerts = req.SecurityAlerts;
        user.GameReminders = req.GameReminders;
        user.DigestFrequency = req.DigestFrequency?.Trim() ?? "Weekly";
        user.Timezone = req.Timezone?.Trim() ?? "Europe/London";
        user.Theme = req.Theme?.Trim() ?? "Dark";
        user.Accent = req.Accent?.Trim() ?? "Misfitz";
        user.CompactLayout = req.CompactLayout;
        user.ShowTips = req.ShowTips;
        user.PublicRoomListing = req.PublicRoomListing;
        user.ShowGameplayStats = req.ShowGameplayStats;

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}
