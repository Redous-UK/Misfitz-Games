using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Effects;

namespace Misfitz_Games.Services;

public class EffectsEngine
{
    private readonly AppDbContext _db;
    private readonly TuyaPlugService _tuya;

    // very simple in-memory cooldown (per instance)
    private static readonly Dictionary<string, DateTimeOffset> _cooldowns = new();

    public EffectsEngine(AppDbContext db, TuyaPlugService tuya)
    {
        _db = db;
        _tuya = tuya;
    }

    public async Task ExecuteEffectAsync(int ownerUserId, Guid effectId, CancellationToken ct = default)
    {
        var effect = await _db.Effects
            .AsNoTracking()
            .Where(e => e.OwnerUserId == ownerUserId && e.Id == effectId && e.IsEnabled)
            .Include(e => e.Targets)
            .FirstOrDefaultAsync(ct);

        if (effect is null)
            throw new InvalidOperationException("Effect not found or disabled.");

        // effect-level cooldown (very basic)
        EnforceCooldown($"effect:{ownerUserId}:{effect.Id}", effect.CooldownSeconds);

        // Expand effect targets into concrete devices (unique devices only)
        var targets = await ExpandTargetsToDevices(ownerUserId, effect, ct);

        // Execute sequentially (MVP). Later: parallel, queueing, priorities.
        foreach (var (dev, seconds) in targets)
        {
            var clamped = Math.Clamp(seconds, 1, dev.MaxPulseSeconds);

            // per-device cooldown (basic)
            EnforceCooldown($"device:{ownerUserId}:{dev.Id}", dev.CooldownSeconds);

            if (dev.Provider != DeviceProvider.Tuya || dev.Capability != DeviceCapability.Switch)
                throw new NotSupportedException($"Unsupported device provider/capability: {dev.Provider}/{dev.Capability}");

            switch (effect.Action)
            {
                case EffectAction.PulseSwitch:
                    await _tuya.SetSwitchAsync(dev.ExternalDeviceId, true, ct);
                    await Task.Delay(TimeSpan.FromSeconds(clamped), ct);
                    await _tuya.SetSwitchAsync(dev.ExternalDeviceId, false, ct);
                    break;

                case EffectAction.SwitchOn:
                    await _tuya.SetSwitchAsync(dev.ExternalDeviceId, true, ct);
                    break;

                case EffectAction.SwitchOff:
                    await _tuya.SetSwitchAsync(dev.ExternalDeviceId, false, ct);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported effect action: {effect.Action}");
            }
        }
    }

    private async Task<List<(Device dev, int seconds)>> ExpandTargetsToDevices(int ownerUserId, Effect effect, CancellationToken ct)
    {
        var output = new List<(Device dev, int seconds)>();
        var seen = new HashSet<Guid>();

        foreach (var t in effect.Targets.OrderBy(x => x.SortOrder))
        {
            var seconds = t.DurationSecondsOverride ?? effect.DurationSeconds;

            if (t.TargetType == EffectTargetType.Device)
            {
                if (t.DeviceId is null) continue;

                var dev = await _db.Devices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.OwnerUserId == ownerUserId && d.Id == t.DeviceId && d.IsEnabled, ct);

                if (dev is null) continue;
                if (seen.Add(dev.Id))
                    output.Add((dev, seconds));
            }
            else if (t.TargetType == EffectTargetType.Group)
            {
                if (t.GroupId is null) continue;

                var memberIds = await _db.DeviceGroupMembers
                    .AsNoTracking()
                    .Where(m => m.GroupId == t.GroupId.Value)
                    .Select(m => m.DeviceId)
                    .ToListAsync(ct);

                if (memberIds.Count == 0) continue;

                var devs = await _db.Devices
                    .AsNoTracking()
                    .Where(d => d.OwnerUserId == ownerUserId && d.IsEnabled && memberIds.Contains(d.Id))
                    .ToListAsync(ct);

                foreach (var dev in devs)
                {
                    if (seen.Add(dev.Id))
                        output.Add((dev, seconds));
                }
            }
        }

        return output;
    }

    private static void EnforceCooldown(string key, int seconds)
    {
        if (seconds <= 0) return;

        var now = DateTimeOffset.UtcNow;
        lock (_cooldowns)
        {
            if (_cooldowns.TryGetValue(key, out var until) && until > now)
                throw new InvalidOperationException($"Cooldown active for {key}. Try again in {(int)(until - now).TotalSeconds}s.");

            _cooldowns[key] = now.AddSeconds(seconds);
        }
    }
}
