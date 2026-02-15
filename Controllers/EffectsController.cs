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
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
[Route("api/effects")]
[Authorize(Policy = "MemberOrAdmin")]
public class EffectsController(AppDbContext db, EffectsEngine engine, EffectsService legacyEffects) : ControllerBase
{
    private readonly AppDbContext _db = db;
    private readonly EffectsEngine _engine = engine;
    private readonly EffectsService _legacyEffects = legacyEffects; // keeps your existing pulse route alive

    // ------------------------------------------------------------------
    // Legacy endpoint (keep working while you migrate to v2)
    // POST /api/effects/plug/pulse   { deviceName: "plug1", seconds: 5 }
    // ------------------------------------------------------------------
    [HttpPost("plug/pulse")]
    public async Task<IActionResult> PulsePlug([FromBody] PulseRequest req)
    {
        await _legacyEffects.PulsePlugAsync(req.DeviceName, req.Seconds);
        return Ok(new { ok = true });
    }

    public record PulseRequest(string DeviceName, int Seconds = 5);

    // ------------------------------------------------------------------
    // Devices
    // GET  /api/effects/devices
    // POST /api/effects/devices   { name, externalDeviceId }
    // DEL  /api/effects/devices/{deviceId}
    // ------------------------------------------------------------------
    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices(CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var devices = await _db.Devices
            .AsNoTracking()
            .Where(d => d.OwnerUserId == uid)
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Provider,
                d.Capability,
                d.ExternalDeviceId,
                d.IsEnabled,
                d.MaxPulseSeconds,
                d.CooldownSeconds,
                d.CreatedUtc
            })
            .ToListAsync(ct);

        return Ok(new { ok = true, devices });
    }

    public record CreateDeviceRequest(string Name, string ExternalDeviceId);

    [HttpPost("devices")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest req, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ExternalDeviceId))
            return BadRequest(new { ok = false, error = "Name and ExternalDeviceId required" });

        var name = req.Name.Trim();
        var externalId = req.ExternalDeviceId.Trim();

        var exists = await _db.Devices.AnyAsync(d => d.OwnerUserId == uid && d.Name == name, ct);
        if (exists) return Conflict(new { ok = false, error = "Device name already exists" });

        var dev = new Device
        {
            OwnerUserId = uid,
            Name = name,
            Provider = DeviceProvider.Tuya,
            Capability = DeviceCapability.Switch,
            ExternalDeviceId = externalId,
            IsEnabled = true
        };

        _db.Devices.Add(dev);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, deviceId = dev.Id });
    }

    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid deviceId, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var dev = await _db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == deviceId, ct);
        if (dev is null) return NotFound(new { ok = false });

        // Remove memberships and effect targets referencing this device
        _db.DeviceGroupMembers.RemoveRange(_db.DeviceGroupMembers.Where(m => m.DeviceId == deviceId));
        _db.EffectTargets.RemoveRange(_db.EffectTargets.Where(t => t.DeviceId == deviceId));

        _db.Devices.Remove(dev);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    // ------------------------------------------------------------------
    // Groups
    // GET  /api/effects/groups
    // POST /api/effects/groups               { name }
    // POST /api/effects/groups/{id}/members  { deviceId }
    // DEL  /api/effects/groups/{id}/members/{deviceId}
    // ------------------------------------------------------------------
    [HttpGet("groups")]
    public async Task<IActionResult> ListGroups(CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var groups = await _db.DeviceGroups
            .AsNoTracking()
            .Where(g => g.OwnerUserId == uid)
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.CreatedUtc })
            .ToListAsync(ct);

        return Ok(new { ok = true, groups });
    }

    public record CreateGroupRequest(string Name);

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name required" });

        var name = req.Name.Trim();

        var exists = await _db.DeviceGroups.AnyAsync(g => g.OwnerUserId == uid && g.Name == name, ct);
        if (exists) return Conflict(new { ok = false, error = "Group name already exists" });

        var g = new DeviceGroup { OwnerUserId = uid, Name = name };
        _db.DeviceGroups.Add(g);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, groupId = g.Id });
    }

    public record SetMemberRequest(Guid DeviceId);

    [HttpPost("groups/{groupId:guid}/members")]
    public async Task<IActionResult> AddGroupMember(Guid groupId, [FromBody] SetMemberRequest req, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var group = await _db.DeviceGroups.FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false, error = "Group not found" });

        var dev = await _db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
        if (dev is null) return NotFound(new { ok = false, error = "Device not found" });

        var exists = await _db.DeviceGroupMembers.AnyAsync(m => m.GroupId == groupId && m.DeviceId == req.DeviceId, ct);
        if (!exists)
        {
            _db.DeviceGroupMembers.Add(new DeviceGroupMember { GroupId = groupId, DeviceId = req.DeviceId });
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { ok = true });
    }

    [HttpDelete("groups/{groupId:guid}/members/{deviceId:guid}")]
    public async Task<IActionResult> RemoveGroupMember(Guid groupId, Guid deviceId, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var group = await _db.DeviceGroups.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false, error = "Group not found" });

        var member = await _db.DeviceGroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, ct);
        if (member is null) return Ok(new { ok = true });

        _db.DeviceGroupMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    // ------------------------------------------------------------------
    // Effects
    // GET  /api/effects/effects
    // POST /api/effects/effects                 { name, action, durationSeconds }
    // POST /api/effects/effects/{id}/targets    { targetType, deviceId?, groupId?, durationSecondsOverride?, sortOrder? }
    // POST /api/effects/effects/{id}/run
    // ------------------------------------------------------------------
    [HttpGet("effects")]
    public async Task<IActionResult> ListEffects(CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var effects = await _db.Effects
            .AsNoTracking()
            .Where(e => e.OwnerUserId == uid)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Action, e.DurationSeconds, e.CooldownSeconds, e.IsEnabled, e.CreatedUtc })
            .ToListAsync(ct);

        return Ok(new { ok = true, effects });
    }

    public record CreateEffectRequest(string Name, EffectAction Action = EffectAction.PulseSwitch, int DurationSeconds = 2);

    [HttpPost("effects")]
    public async Task<IActionResult> CreateEffect([FromBody] CreateEffectRequest req, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name required" });

        var name = req.Name.Trim();

        var exists = await _db.Effects.AnyAsync(e => e.OwnerUserId == uid && e.Name == name, ct);
        if (exists) return Conflict(new { ok = false, error = "Effect name already exists" });

        var effect = new Effect
        {
            OwnerUserId = uid,
            Name = name,
            Action = req.Action,
            DurationSeconds = Math.Clamp(req.DurationSeconds, 1, 30),
            IsEnabled = true
        };

        _db.Effects.Add(effect);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, effectId = effect.Id });
    }

    public record AddTargetRequest(
        EffectTargetType TargetType,
        Guid? DeviceId,
        Guid? GroupId,
        int? DurationSecondsOverride = null,
        int SortOrder = 0);

    [HttpPost("effects/{effectId:guid}/targets")]
    public async Task<IActionResult> AddEffectTarget(Guid effectId, [FromBody] AddTargetRequest req, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        var effect = await _db.Effects.FirstOrDefaultAsync(e => e.OwnerUserId == uid && e.Id == effectId, ct);
        if (effect is null) return NotFound(new { ok = false, error = "Effect not found" });

        if (req.TargetType == EffectTargetType.Device)
        {
            if (req.DeviceId is null) return BadRequest(new { ok = false, error = "DeviceId required" });

            var devExists = await _db.Devices.AnyAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
            if (!devExists) return NotFound(new { ok = false, error = "Device not found" });

            _db.EffectTargets.Add(new EffectTarget
            {
                EffectId = effectId,
                TargetType = EffectTargetType.Device,
                DeviceId = req.DeviceId,
                DurationSecondsOverride = req.DurationSecondsOverride,
                SortOrder = req.SortOrder
            });
        }
        else if (req.TargetType == EffectTargetType.Group)
        {
            if (req.GroupId is null) return BadRequest(new { ok = false, error = "GroupId required" });

            var grpExists = await _db.DeviceGroups.AnyAsync(g => g.OwnerUserId == uid && g.Id == req.GroupId, ct);
            if (!grpExists) return NotFound(new { ok = false, error = "Group not found" });

            _db.EffectTargets.Add(new EffectTarget
            {
                EffectId = effectId,
                TargetType = EffectTargetType.Group,
                GroupId = req.GroupId,
                DurationSecondsOverride = req.DurationSecondsOverride,
                SortOrder = req.SortOrder
            });
        }
        else
        {
            return BadRequest(new { ok = false, error = "Invalid TargetType" });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("effects/{effectId:guid}/run")]
    public async Task<IActionResult> RunEffect(Guid effectId, CancellationToken ct)
    {
        if (!TryGetAppUserId(out var uid))
            return Unauthorized(new { ok = false, error = "Missing user id claim" });

        await _engine.ExecuteEffectAsync(uid, effectId, ct);
        return Ok(new { ok = true });
    }

    // ------------------------------------------------------------------
    // Helper: resolve your App user id from claims
    // ------------------------------------------------------------------
    private bool TryGetAppUserId(out int uid)
    {

        // Adjust this if your claim name differs.
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("uid") ??
            User.FindFirstValue("userId") ??
            User.FindFirstValue("id");

        return int.TryParse(raw, out uid);
    }
}
