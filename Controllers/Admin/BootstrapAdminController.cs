using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;

namespace Misfitz_Games.Controllers.Admin;

[ApiController]
public class BootstrapAdminController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _cfg = cfg;
    private readonly IWebHostEnvironment _env = env;

    // Set this in Render:
    // ADMIN_BOOTSTRAP_KEY = "some-long-random-string"
    private bool IsKeyValid(string? key)
    {
        var expected = _cfg["ADMIN_BOOTSTRAP_KEY"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return string.Equals(key, expected);
    }

    // Safety: allow bootstrap only if explicitly enabled OR in Development
    // You can choose either/both constraints.
    private bool IsBootstrapEnabled()
    {
        // Recommended: require explicit enable in production.
        // Set ENABLE_BOOTSTRAP_ADMIN=true temporarily when you need it.
        var enabled = string.Equals(_cfg["ENABLE_BOOTSTRAP_ADMIN"], "true", StringComparison.OrdinalIgnoreCase);
        return _env.IsDevelopment() || enabled;
    }

    // Optional extra safety: only allow if there are currently NO admins
    private async Task<bool> NoAdminsExist(CancellationToken ct)
        => !await _db.Users.AnyAsync(u => u.Role == "admin", ct);

    public record SetRoleReq(string Role);

    [HttpGet("/bootstrap/users")]
    public async Task<IActionResult> ListUsers([FromQuery] string? key, CancellationToken ct)
    {
        if (!IsBootstrapEnabled()) return NotFound(); // hide endpoint unless enabled
        if (!IsKeyValid(key)) return Unauthorized(new { ok = false, error = "Invalid bootstrap key." });

        // If you want the stricter rule:
        // if (!await NoAdminsExist(ct)) return Forbid();

        var users = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                id = u.Id,
                username = u.Username,
                role = u.Role,
                createdUtc = u.CreatedUtc,
                lastLoginUtc = u.LastLoginUtc
            })
            .ToListAsync(ct);

        return Ok(new { ok = true, users });
    }

    [HttpPost("/bootstrap/users/{id:guid}/role")]
    public async Task<IActionResult> SetRole([FromRoute] Guid id, [FromQuery] string? key, [FromBody] SetRoleReq req, CancellationToken ct)
    {
        if (!IsBootstrapEnabled()) return NotFound();
        if (!IsKeyValid(key)) return Unauthorized(new { ok = false, error = "Invalid bootstrap key." });

        var role = (req.Role ?? "").Trim().ToLowerInvariant();

        if (role != "admin" && role != "member" && role != "guest")
        {
            return BadRequest(new
            {
                ok = false,
                error = "Role must be 'admin', 'member', or 'guest'."
            });
        }

        // Optional stricter rule:
        // if (!await NoAdminsExist(ct)) return Forbid();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null) return NotFound(new { ok = false, error = "User not found." });

        user.Role = role;
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, id = user.Id, username = user.Username, role = user.Role });
    }
}