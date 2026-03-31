using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Controllers;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Effects;
using Misfitz_Games.Services;
using Misfitz_Games.Services.Effects;

namespace Misfitz_Games.Controllers.Effects;

[ApiController]
[Route("api/effects")]
[Authorize(Policy = "MemberOrAdmin")]
public class EffectsController(AppDbContext db, EffectsEngine engine, EffectsService legacyEffects) : ControllerBase
{
    //private readonly AppDbContext _db = db;
    private readonly EffectsEngine _engine = engine;
    private readonly EffectsService _legacyEffects = legacyEffects; // keeps your existing pulse route alive

    // ------------------------------------------------------------------
    // Legacy endpoint (keep working while you migrate to v2)
    // POST /api/effects/plug/pulse   { deviceName: "plug1", seconds: 5 }
    // ------------------------------------------------------------------
    [HttpPost("plug/pulse")]
    public async Task<IActionResult> PulsePlug([FromBody] PulseRequest req)
    {
        await _legacyEffects.PulsePlugAsync(req.DeviceName, req.Seconds, HttpContext.RequestAborted);
        return Ok(new { ok = true });
    }

    public record PulseRequest(string DeviceName, int Seconds = 2);

    // ------------------------------------------------------------------
    // Devices
    // GET  /api/effects/devices
    // POST /api/effects/devices   { name, externalDeviceId }
    // DEL  /api/effects/devices/{deviceId}
    // ------------------------------------------------------------------
    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices(CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var devices = await db.Devices
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
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ExternalDeviceId))
            return BadRequest(new { ok = false, error = "Name and ExternalDeviceId required" });

        var name = req.Name.Trim();
        var externalId = req.ExternalDeviceId.Trim();

        var exists = await db.Devices.AnyAsync(d => d.OwnerUserId == uid && d.Name == name, ct);
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

        db.Devices.Add(dev);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, deviceId = dev.Id });
    }

    [HttpDelete("devices/{deviceId:guid}")]
    public async Task<IActionResult> DeleteDevice(Guid deviceId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var dev = await db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == deviceId, ct);
        if (dev is null) return NotFound(new { ok = false });

        // Remove memberships and effect targets referencing this device
        db.DeviceGroupMembers.RemoveRange(db.DeviceGroupMembers.Where(m => m.DeviceId == deviceId));
        db.EffectTargets.RemoveRange(db.EffectTargets.Where(t => t.DeviceId == deviceId));

        db.Devices.Remove(dev);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [HttpPost("devices/sync")]
    public async Task<IActionResult> SyncDevicesFromTuya(CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        try
        {
            // ✅ Prefer DB link (remove ENV dependency)
            var link = await db.TuyaLinks.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == uid, ct);

            if (link is null || string.IsNullOrWhiteSpace(link.TuyaUid))
                return BadRequest(new { ok = false, error = "No Tuya account linked for this user." });

            var tuyaUid = link.TuyaUid;

            // Pull from Tuya
            var items = await HttpContext.RequestServices
                .GetRequiredService<TuyaPlugService>()
                .ListDevicesByUidAsync(tuyaUid, ct);

            var added = 0;
            var updated = 0;

            foreach (var d in items)
            {
                var externalId = d.GetProperty("id").GetString() ?? "";
                var name = d.TryGetProperty("name", out var nm) ? (nm.GetString() ?? externalId) : externalId;

                if (string.IsNullOrWhiteSpace(externalId)) continue;

                var existing = await db.Devices.FirstOrDefaultAsync(
                    x => x.OwnerUserId == uid && x.ExternalDeviceId == externalId, ct);

                if (existing is null)
                {
                    db.Devices.Add(new Device
                    {
                        OwnerUserId = uid,
                        Name = name.Trim(),
                        Provider = DeviceProvider.Tuya,
                        Capability = DeviceCapability.Switch,
                        ExternalDeviceId = externalId,
                        IsEnabled = true
                    });
                    added++;
                }
                else
                {
                    if (!string.Equals(existing.Name, name, StringComparison.Ordinal))
                    {
                        existing.Name = name.Trim();
                        updated++;
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            return Ok(new { ok = true, tuyaUid, added, updated, totalTuya = items.Length });
        }
        catch (Exception ex)
        {
            HttpContext.RequestServices
                .GetRequiredService<ILogger<EffectsController>>()
                .LogError(ex, "SyncDevicesFromTuya failed. uid={Uid}", uid);

            return StatusCode(500, new { ok = false, error = ex.Message, type = ex.GetType().Name });
        }
    }

    [Authorize(Roles = "admin")]
    [HttpGet("tuya")]
    public IActionResult GetTuyaLinks()
    {
        try
        {
            var data = db.TuyaLinks
                .Select(x => new
                {
                    x.UserId,
                    x.TuyaUid,
                    x.AccessTokenEnc,
                    x.RefreshTokenEnc,
                    x.AccessTokenExpiresUtc
                })
                .ToList();

            return Ok(data);
        }
        catch (Exception ex)
        {
            // log ex in your logger if you have it
            return Problem(detail: ex.Message);
        }
    }

    [Authorize(Roles = "admin")]
    [HttpGet("/api/tuya/link")]
    public IActionResult GetMyTuyaLink()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        if (!Guid.TryParse(userId, out var uid)) return BadRequest("Bad user id");
        var link = db.TuyaLinks.FirstOrDefault(x => x.UserId == uid);
        return Ok(new { hasLink = link != null, link });
    }

    public record LinkTuyaUidRequest(string TuyaUid);

    [HttpPost("tuya/link")]
    public async Task<IActionResult> LinkTuyaUid([FromBody] LinkTuyaUidRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.TuyaUid))
            return BadRequest(new { ok = false, error = "TuyaUid required" });

        var link = await db.TuyaLinks.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (link is null)
        {
            link = new TuyaAccountLink { UserId = uid };
            db.TuyaLinks.Add(link);
        }

        link.TuyaUid = req.TuyaUid.Trim();
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [Authorize(Roles = "admin")]
    [HttpPost("/admin/db/tuya/delete-test")]
    public IActionResult DeleteTest()
    {
        var test = db.TuyaLinks.FirstOrDefault(x => x.TuyaUid == "TEST_UID");
        if (test == null) return Ok(new { ok = true, deleted = false });

        db.TuyaLinks.Remove(test);
        db.SaveChanges();
        return Ok(new { ok = true, deleted = true });
    }

    public record SwitchRequest(Guid DeviceId, bool On);

    [HttpPost("devices/switch")]
    public async Task<IActionResult> SwitchDevice([FromBody] SwitchRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var dev = await db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
        if (dev is null) return NotFound(new { ok = false, error = "Device not found" });

        if (dev.Provider != DeviceProvider.Tuya)
            return BadRequest(new { ok = false, error = "Device is not Tuya" });

        await HttpContext.RequestServices
            .GetRequiredService<TuyaPlugService>()
            .SetSwitchAsync(dev.ExternalDeviceId, req.On, ct);

        return Ok(new { ok = true });
    }

    // PATCH /api/effects/{effectId}
    // Allows editing name/action/duration/cooldown/enabled
    [HttpPatch("{effectId:guid}")]
    public async Task<IActionResult> PatchEffect(Guid effectId, [FromBody] PatchEffectRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var effect = await db.Effects.FirstOrDefaultAsync(e => e.OwnerUserId == uid && e.Id == effectId, ct);
        if (effect is null) return NotFound(new { ok = false, error = "Effect not found" });

        if (req.Name is not null)
        {
            var name = req.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { ok = false, error = "Name cannot be blank" });

            // Optional: prevent duplicates
            var exists = await db.Effects.AnyAsync(e => e.OwnerUserId == uid && e.Id != effectId && e.Name == name, ct);
            if (exists) return Conflict(new { ok = false, error = "Effect name already exists" });

            effect.Name = name;
        }

        if (req.Action is not null) effect.Action = req.Action.Value;
        if (req.DurationSeconds is not null) effect.DurationSeconds = Math.Clamp(req.DurationSeconds.Value, 1, 30);
        if (req.CooldownSeconds is not null) effect.CooldownSeconds = Math.Clamp(req.CooldownSeconds.Value, 0, 600);
        if (req.IsEnabled is not null) effect.IsEnabled = req.IsEnabled.Value;

        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    public record PatchEffectRequest(
        string? Name,
        EffectAction? Action,
        int? DurationSeconds,
        int? CooldownSeconds,
        bool? IsEnabled
    );

    // DELETE /api/effects/{effectId}
    [HttpDelete("{effectId:guid}")]
    public async Task<IActionResult> DeleteEffect(Guid effectId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var effect = await db.Effects
            .Include(e => e.Targets)
            .FirstOrDefaultAsync(e => e.OwnerUserId == uid && e.Id == effectId, ct);

        if (effect is null) return NotFound(new { ok = false, error = "Effect not found" });

        db.EffectTargets.RemoveRange(effect.Targets);
        db.Effects.Remove(effect);

        await db.SaveChangesAsync(ct);
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
        var uid = GetAppUserIdOrThrow();

        var groups = await db.DeviceGroups
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
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name required" });

        var name = req.Name.Trim();

        var exists = await db.DeviceGroups.AnyAsync(g => g.OwnerUserId == uid && g.Name == name, ct);
        if (exists) return Conflict(new { ok = false, error = "Group name already exists" });

        var g = new DeviceGroup { OwnerUserId = uid, Name = name };
        db.DeviceGroups.Add(g);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, groupId = g.Id });
    }

    public record SetMemberRequest(Guid DeviceId);

    [HttpPost("groups/{groupId:guid}/members")]
    public async Task<IActionResult> AddGroupMember(Guid groupId, [FromBody] SetMemberRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var group = await db.DeviceGroups.FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false, error = "Group not found" });

        var dev = await db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
        if (dev is null) return NotFound(new { ok = false, error = "Device not found" });

        var exists = await db.DeviceGroupMembers.AnyAsync(m => m.GroupId == groupId && m.DeviceId == req.DeviceId, ct);
        if (!exists)
        {
            db.DeviceGroupMembers.Add(new DeviceGroupMember { GroupId = groupId, DeviceId = req.DeviceId });
            await db.SaveChangesAsync(ct);
        }

        return Ok(new { ok = true });
    }

    [HttpDelete("groups/{groupId:guid}/members/{deviceId:guid}")]
    public async Task<IActionResult> RemoveGroupMember(Guid groupId, Guid deviceId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var group = await db.DeviceGroups.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerUserId == uid && g.Id == groupId, ct);
        if (group is null) return NotFound(new { ok = false, error = "Group not found" });

        var member = await db.DeviceGroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.DeviceId == deviceId, ct);
        if (member is null) return Ok(new { ok = true });

        db.DeviceGroupMembers.Remove(member);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    // ------------------------------------------------------------------
    // Effects
    // GET  /api/effects
    // POST /api/effects                 { name, action, durationSeconds }
    // GET  /api/effects/{effectId:guid} list of targets by user
    // POST /api/effects/{id}/targets    { targetType, deviceId?, groupId?, durationSecondsOverride?, sortOrder? }
    // POST /api/effects/{id}/run
    // DEL  /api/effects/targets/{targetId:guid}
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ListEffects(CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var effects = await db.Effects
            .AsNoTracking()
            .Where(e => e.OwnerUserId == uid)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Action, e.DurationSeconds, e.CooldownSeconds, e.IsEnabled, e.CreatedUtc })
            .ToListAsync(ct);

        return Ok(new { ok = true, effects });
    }

    public record CreateEffectRequest(string Name, EffectAction Action = EffectAction.PulseSwitch, int DurationSeconds = 2);

    [HttpPost]
    public async Task<IActionResult> CreateEffect([FromBody] CreateEffectRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name required" });

        var name = req.Name.Trim();

        var exists = await db.Effects.AnyAsync(e => e.OwnerUserId == uid && e.Name == name, ct);
        if (exists) return Conflict(new { ok = false, error = "Effect name already exists" });

        var effect = new Effect
        {
            OwnerUserId = uid,
            Name = name,
            Action = req.Action,
            DurationSeconds = Math.Clamp(req.DurationSeconds, 1, 30),
            IsEnabled = true
        };

        db.Effects.Add(effect);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true, effectId = effect.Id });
    }

    public record AddTargetRequest(
        EffectTargetType TargetType,
        Guid? DeviceId,
        Guid? GroupId,
        int? DurationSecondsOverride = null,
        int SortOrder = 0);

    [HttpPost("{effectId:guid}/targets")]
    public async Task<IActionResult> AddEffectTarget(Guid effectId, [FromBody] AddTargetRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var effect = await db.Effects.FirstOrDefaultAsync(e => e.OwnerUserId == uid && e.Id == effectId, ct);
        if (effect is null) return NotFound(new { ok = false, error = "Effect not found" });

        if (req.TargetType == EffectTargetType.Device)
        {
            if (req.DeviceId is null) return BadRequest(new { ok = false, error = "DeviceId required" });

            var devExists = await db.Devices.AnyAsync(d => d.OwnerUserId == uid && d.Id == req.DeviceId, ct);
            if (!devExists) return NotFound(new { ok = false, error = "Device not found" });

            db.EffectTargets.Add(new EffectTarget
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

            var grpExists = await db.DeviceGroups.AnyAsync(g => g.OwnerUserId == uid && g.Id == req.GroupId, ct);
            if (!grpExists) return NotFound(new { ok = false, error = "Group not found" });

            db.EffectTargets.Add(new EffectTarget
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

        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    [HttpPost("{effectId:guid}/run")]
    public async Task<IActionResult> RunEffect(Guid effectId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        try
        {
            await _engine.ExecuteEffectAsync(uid, effectId, ct);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            // log it so Render logs show full details
            HttpContext.RequestServices
                .GetRequiredService<ILogger<EffectsController>>()
                .LogError(ex, "RunEffect failed. effectId={EffectId} uid={Uid}", effectId, uid);

            // return something parseable to the browser
            return StatusCode(500, new { ok = false, error = ex.Message, type = ex.GetType().Name });
        }
    }

    [HttpGet("{effectId:guid}")]
    public async Task<IActionResult> GetEffect(Guid effectId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var effect = await db.Effects
            .AsNoTracking()
            .Include(e => e.Targets)
                .ThenInclude(t => t.Device)
            .Include(e => e.Targets)
                .ThenInclude(t => t.Group)
            .FirstOrDefaultAsync(e => e.OwnerUserId == uid && e.Id == effectId, ct);

        if (effect is null)
            return NotFound(new { ok = false, error = "Effect not found" });

        var targets = effect.Targets
            .OrderBy(t => t.SortOrder)
            .Select(t => new
            {
                t.Id,
                targetType = (int)t.TargetType,
                t.DeviceId,
                deviceName = t.Device?.Name,
                t.GroupId,
                groupName = t.Group?.Name,
                t.DurationSecondsOverride,
                t.SortOrder
            })
            .ToList();

        return Ok(new
        {
            ok = true,
            effect = new
            {
                effect.Id,
                effect.Name,
                action = (int)effect.Action,
                effect.DurationSeconds,
                effect.CooldownSeconds,
                effect.IsEnabled,
                effect.CreatedUtc,
                targets
            }
        });
    }

    [HttpDelete("targets/{targetId:guid}")]
    public async Task<IActionResult> DeleteTarget(Guid targetId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var target = await db.EffectTargets
            .Include(t => t.Effect)
            .FirstOrDefaultAsync(t => t.Id == targetId && t.Effect.OwnerUserId == uid, ct);

        if (target is null)
            return NotFound(new { ok = false, error = "Target not found" });

        db.EffectTargets.Remove(target);
        await db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }



    // ------------------------------------------------------------------
    // Helper: resolve your App user id from claims
    // ------------------------------------------------------------------
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
