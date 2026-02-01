using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Services;
using System.Security.Claims;

namespace Misfitz_Games.Controllers;

[ApiController]
public class MemberController(AppDbContext db, IRoomStateStore store) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IRoomStateStore _store = store;

    public record RegisterReq(string Name, string Password);
    public record LoginReq(string Name, string Password);

    // -------- Room helpers --------
    private static string NewNumericCode()
        => Random.Shared.Next(0, 100_000_000).ToString("D8");

    private async Task<(Guid roomId, string roomCode)> CreateRoomForUserAsync(string username, CancellationToken ct)
    {
        var roomId = Guid.NewGuid();

        string code = "";
        for (var i = 0; i < 25; i++)
        {
            var candidate = NewNumericCode();
            if (await _store.TryReserveRoomCodeAsync(candidate, roomId, ct))
            {
                code = candidate;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Failed to allocate a room code. Try again.");

        var room = new RoomDto(
            RoomId: roomId,
            Name: $"{username}'s Room",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RoomCode: code
        );

        try
        {
            await _store.SaveRoomAsync(room, ct);

            var state = new RoomState(
                RoomId: room.RoomId,
                RoomName: room.Name,
                ActiveGame: GameType.None,
                GameState: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow
            );

            await _store.SaveStateAsync(state, ct);
            return (roomId, code);
        }
        catch
        {
            await _store.ReleaseRoomCodeAsync(code, ct);
            throw;
        }
    }

    // -------- Auth endpoints --------

    [HttpPost("/member/register")]
    public async Task<IActionResult> Register([FromBody] RegisterReq req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length < 3 || name.Length > 32)
            return BadRequest(new { ok = false, error = "Name must be 3-32 chars." });

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { ok = false, error = "Password must be 6+ chars." });

        var nameUpper = name.ToUpperInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Username.ToUpper() == nameUpper, ct);
        if (exists) return Conflict(new { ok = false, error = "Username already exists." });

        var (hash, salt) = PasswordHasher.HashPassword(req.Password);

        var user = new User
        {
            Username = name,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "member",
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [HttpPost("/member/login")]
    public async Task<IActionResult> Login([FromBody] LoginReq req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        var nameUpper = name.ToUpperInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToUpper() == nameUpper, ct);
        if (user == null)
            return Unauthorized(new { ok = false, error = "Invalid credentials." });

        if (!PasswordHasher.Verify(req.Password ?? "", user.PasswordHash, user.PasswordSalt))
            return Unauthorized(new { ok = false, error = "Invalid credentials." });

        user.LastLoginUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true }
        );

        // Auto-create a room for this user
        var (newRoomId, newRoomCode) = await CreateRoomForUserAsync(user.Username, ct);

        return Ok(new
        {
            ok = true,
            userId = user.Id,
            name = user.Username,
            role = user.Role,
            roomId = newRoomId,
            roomCode = newRoomCode
        });
    }

    [HttpPost("/member/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { ok = true });
    }

    [HttpGet("/member/me")]
    public IActionResult Me()
    {
        if (!(User?.Identity?.IsAuthenticated ?? false))
            return Ok(new { ok = true, isAuth = false });

        var name = User.FindFirstValue(ClaimTypes.Name);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Ok(new { ok = true, isAuth = true, name, role, userId = id, isMember = (role == "member") });
    }
}