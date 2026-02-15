using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Effects;

namespace Misfitz_Games.Controllers;

[ApiController]
[Route("api/effects/v2/groups")]
[Authorize(Policy = "MemberOrAdmin")]
public class GroupsController(AppDbContext db) : ControllerBase
{
    private readonly AppDbContext _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var groups = await _db.DeviceGroups
            .AsNoTracking()
            .Where(g => g.OwnerUserId == uid)
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.CreatedUtc })
            .ToListAsync(ct);

        return Ok(new { ok = true, groups });
    }

    public record CreateGroupRequest(string Name);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name required" });

        var exists = await _db.DeviceGroups.AnyAsync(g => g.OwnerUserId == uid && g.Name == req.Name, ct);
        if (exists) return Conflict(new { ok = false, error = "Group name already exists" });

        var g = new DeviceGroup { OwnerUserId = uid, Name = req.Name.Trim() };
        _db.DeviceGroups.Add(g);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, groupId = g.Id });
    }

    public record SetMemberRequest(Guid DeviceId);

    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid groupId, [FromBody] SetMemberRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var group = await _db.DeviceGroups.FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false, error = "Group not found" });

        var dev = await _db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
        if (dev is null) return NotFound(new { ok = false, error = "Device not found" });

        var exists = await _db.DeviceGroupMembers.AnyAsync(m => m.GroupId == groupId && m.DeviceId == req.DeviceId, ct);
        if (exists) return Ok(new { ok = true }); // idempotent

        _db.DeviceGroupMembers.Add(new DeviceGroupMember { GroupId = groupId, DeviceId = req.DeviceId });
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [HttpDelete("{groupId:guid}/members/{deviceId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid deviceId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var group = await _db.DeviceGroups.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false });

        var member = await _db.DeviceGroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, ct);
        if (member is null) return Ok(new { ok = true });

        _db.DeviceGroupMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    private Guid GetAppUserIdOrThrow()
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("uid") ??
            User.FindFirstValue("userId") ??
            User.FindFirstValue("id");

        if (!Guid.TryParse(raw, out var uid))
            throw new InvalidOperationException("Missing/invalid user id claim.");

        return uid;
    }
}
