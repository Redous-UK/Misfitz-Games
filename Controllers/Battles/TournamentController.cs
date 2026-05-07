using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Battles;
using Misfitz_Games.Models.Battles.Requests;

namespace Misfitz_Games.Controllers.Battles;

[ApiController]
[Authorize]
public sealed class TournamentsController(AppDbContext db) : ControllerBase
{
    [HttpGet("/api/tournaments")]
    public async Task<IActionResult> ListTournaments(CancellationToken ct)
    {
        var tournamentRows = await db.Tournaments
            .AsNoTracking()
            .ToListAsync(ct);

        var signupCounts = await db.TournamentSignups
            .AsNoTracking()
            .GroupBy(x => x.TournamentId)
            .Select(g => new
            {
                TournamentId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.TournamentId, x => x.Count, ct);

        var tournaments = tournamentRows
            .OrderBy(x => x.StartsAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.RequiredSignups,
                x.Prize,
                x.Description,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.CreatedByUserId,
                x.CreatedAtUtc,
                x.Status,
                SignupCount = signupCounts.TryGetValue(x.Id, out var count) ? count : 0
            })
            .ToList();

        return Ok(new { ok = true, tournaments });
    }

    [HttpGet("/api/tournaments/{id:guid}")]
    public async Task<IActionResult> GetTournament(Guid id, CancellationToken ct)
    {
        var tournament = await db.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tournament is null)
            return NotFound(new { ok = false, error = "Tournament not found." });

        var signupRows = await db.TournamentSignups
            .AsNoTracking()
            .Where(s => s.TournamentId == id)
            .ToListAsync(ct);

        var signups = signupRows
            .OrderBy(s => s.SignedUpAtUtc)
            .Select(s => new
            {
                s.Id,
                s.UserId,
                s.SignedUpAtUtc
            })
            .ToList();

        return Ok(new
        {
            ok = true,
            tournament = new
            {
                tournament.Id,
                tournament.Title,
                tournament.RequiredSignups,
                tournament.Prize,
                tournament.Description,
                tournament.StartsAtUtc,
                tournament.EndsAtUtc,
                tournament.CreatedByUserId,
                tournament.CreatedAtUtc,
                tournament.Status,
                SignupCount = signups.Count,
                Signups = signups
            }
        });
    }

    [HttpPost("/api/tournaments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto req, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var title = req.Title?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { ok = false, error = "Tournament title is required." });

        if (req.RequiredSignups <= 0)
            return BadRequest(new { ok = false, error = "Required signups must be greater than zero." });

        if (req.StartsAtUtc == default)
            return BadRequest(new { ok = false, error = "Start date is required." });

        if (req.EndsAtUtc == default || req.EndsAtUtc <= req.StartsAtUtc)
            return BadRequest(new { ok = false, error = "End date must be after start date." });

        var status = NormalizeStatus(req.Status);

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = req.Name?.Trim() ?? "",
            Game = req.Game?.Trim() ?? "",
            Title = title,
            RequiredSignups = req.RequiredSignups,
            Prize = req.Prize?.Trim(),
            Description = req.Description?.Trim(),
            StartsAtUtc = req.StartsAtUtc,
            EndsAtUtc = req.EndsAtUtc,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = status
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, tournament });
    }

    [HttpPut("/api/tournaments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateTournament(Guid id, [FromBody] UpdateTournamentDto req, CancellationToken ct)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tournament is null)
            return NotFound(new { ok = false, error = "Tournament not found." });

        var title = req.Title?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { ok = false, error = "Tournament title is required." });

        if (req.RequiredSignups <= 0)
            return BadRequest(new { ok = false, error = "Required signups must be greater than zero." });

        if (req.StartsAtUtc == default)
            return BadRequest(new { ok = false, error = "Start date is required." });

        if (req.EndsAtUtc == default || req.EndsAtUtc <= req.StartsAtUtc)
            return BadRequest(new { ok = false, error = "End date must be after start date." });

        tournament.Title = title;
        tournament.RequiredSignups = req.RequiredSignups;
        tournament.Prize = req.Prize?.Trim();
        tournament.Description = req.Description?.Trim();
        tournament.StartsAtUtc = req.StartsAtUtc;
        tournament.EndsAtUtc = req.EndsAtUtc;
        tournament.Status = NormalizeStatus(req.Status);

        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, tournament });
    }

    [HttpDelete("/api/tournaments/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteTournament(Guid id, CancellationToken ct)
    {
        var tournament = await db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tournament is null)
            return NotFound(new { ok = false, error = "Tournament not found." });

        db.Tournaments.Remove(tournament);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [HttpPost("/api/tournaments/{id:guid}/signup")]
    [Authorize(Policy = "MemberOrAdmin")]
    public async Task<IActionResult> Signup(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var tournament = await db.Tournaments.FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tournament is null)
            return NotFound(new { ok = false, error = "Tournament not found." });

        if (tournament.Status is not "open" and not "draft")
            return BadRequest(new { ok = false, error = "Tournament is not open for signups." });

        var alreadySignedUp = await db.TournamentSignups
            .AnyAsync(x => x.TournamentId == id && x.UserId == userId, ct);

        if (alreadySignedUp)
            return BadRequest(new { ok = false, error = "You are already signed up for this tournament." });

        var currentCount = await db.TournamentSignups.CountAsync(x => x.TournamentId == id, ct);

        if (currentCount >= tournament.RequiredSignups)
            return BadRequest(new { ok = false, error = "Tournament is full." });

        var signup = new TournamentSignup
        {
            Id = Guid.NewGuid(),
            TournamentId = id,
            UserId = userId,
            SignedUpAtUtc = DateTimeOffset.UtcNow
        };

        db.TournamentSignups.Add(signup);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, signup });
    }

    [HttpDelete("/api/tournaments/{id:guid}/signup")]
    [Authorize(Policy = "MemberOrAdmin")]
    public async Task<IActionResult> CancelSignup(Guid id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { ok = false, error = "Invalid user id." });

        var signup = await db.TournamentSignups
            .FirstOrDefaultAsync(x => x.TournamentId == id && x.UserId == userId, ct);

        if (signup is null)
            return NotFound(new { ok = false, error = "Signup not found." });

        db.TournamentSignups.Remove(signup);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("userId")
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out userId);
    }

    private static string NormalizeStatus(string? status)
    {
        var value = (status ?? "draft").Trim().ToLowerInvariant();

        return value switch
        {
            "draft" => "draft",
            "open" => "open",
            "active" => "active",
            "completed" => "completed",
            "cancelled" => "cancelled",
            _ => "draft"
        };
    }
}