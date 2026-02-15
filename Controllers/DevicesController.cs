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
[Route("api/effects/v2/devices")]
[Authorize(Policy = "MemberOrAdmin")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest req, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ExternalDeviceId))
            return BadRequest(new { ok = false, error = "Name and ExternalDeviceId required" });

        var exists = await _db.Devices.AnyAsync(d => d.OwnerUserId == uid && d.Name == req.Name, ct);
        if (exists) return Conflict(new { ok = false, error = "Device name already exists" });

        var dev = new Device
        {
            OwnerUserId = uid,
            Name = req.Name.Trim(),
            Provider = DeviceProvider.Tuya,
            Capability = DeviceCapability.Switch,
            ExternalDeviceId = req.ExternalDeviceId.Trim()
        };

        _db.Devices.Add(dev);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, deviceId = dev.Id });
    }

    [HttpDelete("{deviceId:guid}")]
    public async Task<IActionResult> Delete(Guid deviceId, CancellationToken ct)
    {
        var uid = GetAppUserIdOrThrow();

        var dev = await _db.Devices.FirstOrDefaultAsync(d => d.OwnerUserId == uid && d.Id == deviceId, ct);
        if (dev is null) return NotFound(new { ok = false });

        // Remove group memberships and targets
        _db.DeviceGroupMembers.RemoveRange(_db.DeviceGroupMembers.Where(m => m.DeviceId == deviceId));
        _db.EffectTargets.RemoveRange(_db.EffectTargets.Where(t => t.DeviceId == deviceId));

        _db.Devices.Remove(dev);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    private int GetAppUserIdOrThrow()
    {
        // Adjust this if your claim name differs.
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("uid") ??
            User.FindFirstValue("userId");

        if (!int.TryParse(raw, out var uid))
            throw new InvalidOperationException("Missing/invalid user id claim. Ensure you issue NameIdentifier (int User.Id).");

        return uid;
    }
}
