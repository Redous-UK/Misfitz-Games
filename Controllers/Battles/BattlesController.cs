using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Battles;
using Misfitz_Games.Models.Battles.Requests;

namespace Misfitz_Games.Controllers.Battles;

[ApiController]
[Authorize]
public sealed class BattlesController(AppDbContext db) : ControllerBase
{
    [HttpGet("/api/battles")]
    public async Task<IActionResult> ListBattles(CancellationToken ct)
    {
        var battlesRows = await db.BattleEvents
            .AsNoTracking()
            .ToListAsync(ct);

        var battles = battlesRows
            .OrderBy(x => x.StartsAtUtc)
            .ToList();


        return Ok(new { ok = true, battles });
    }

    [HttpGet("/api/battles/{id:guid}")]
    public async Task<IActionResult> GetBattle(Guid id, CancellationToken ct)
    {
        var battle = await db.BattleEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return battle is null
            ? NotFound(new { ok = false, error = "Battle not found." })
            : Ok(battle);
    }

    [HttpGet("/api/battles/me")]
    public async Task<IActionResult> MyBattles(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var battlesRows = await db.BattleEvents
            .AsNoTracking()
            .Where(x => x.RequestedByUserId == userId || x.OwnerUserId == userId)
            .ToListAsync(ct);

        var battles = battlesRows
            .OrderBy(x => x.StartsAtUtc)
            .ToList();

        return Ok(new { ok = true, battles });
    }

    [HttpPost("/api/battles/request")]
    public async Task<IActionResult> RequestBattle([FromBody] RequestBattleDto req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var title = (req.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { ok = false, error = "Title is required." });

        if (req.StartsAtUtc == default)
            return BadRequest(new { ok = false, error = "Start date is required." });

        if (req.EndsAtUtc is not null && req.EndsAtUtc <= req.StartsAtUtc)
            return BadRequest(new { ok = false, error = "End date must be after start date." });

        var now = DateTimeOffset.UtcNow;

        var battle = new BattleEvent
        {
            Id = Guid.NewGuid(),
            Title = title,
            OpponentName = req.OpponentName?.Trim(),
            Description = req.Description?.Trim(),
            StartsAtUtc = req.StartsAtUtc,
            EndsAtUtc = req.EndsAtUtc,
            Status = "pending",
            RequestedByUserId = userId,
            OwnerUserId = userId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.BattleEvents.Add(battle);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, battle });
    }

    [HttpPut("/api/battles/{id:guid}")]
    public async Task<IActionResult> UpdateBattle(Guid id, [FromBody] UpdateBattleDto req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var battle = await db.BattleEvents.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (battle is null)
            return NotFound(new { ok = false, error = "Battle not found." });

        var isAdmin = User.IsInRole("admin");
        var isOwner = battle.OwnerUserId == userId || battle.RequestedByUserId == userId;

        if (!isAdmin && !isOwner)
            return Forbid();

        var title = req.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { ok = false, error = "Title is required." });

        if (req.StartsAtUtc == default)
            return BadRequest(new { ok = false, error = "Start date is required." });

        if (req.EndsAtUtc is not null && req.EndsAtUtc <= req.StartsAtUtc)
            return BadRequest(new { ok = false, error = "End date must be after start date." });

        battle.Title = title;
        battle.RoomRef = req.RoomRef?.Trim();
        battle.Description = req.Description?.Trim();
        battle.StartsAtUtc = req.StartsAtUtc;
        battle.EndsAtUtc = req.EndsAtUtc;
        battle.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, battle });
    }

    [HttpPost("/api/battles/{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateBattleStatusDto req, CancellationToken ct)
    {
        var battle = await db.BattleEvents.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (battle is null)
            return NotFound(new { ok = false, error = "Battle not found." });

        var status = (req.Status ?? "").Trim().ToLowerInvariant();

        if (status is not ("pending" or "approved" or "declined" or "completed"))
            return BadRequest(new { ok = false, error = "Invalid battle status." });

        battle.Status = status;
        battle.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, battle });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out userId);
    }
}