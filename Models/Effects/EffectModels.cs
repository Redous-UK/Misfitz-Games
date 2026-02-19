using System;
using System.Collections.Generic;

namespace Misfitz_Games.Models.Effects;

public enum EffectAction
{
    PulseSwitch = 1,
    SwitchOn = 2,
    SwitchOff = 3,
}

public enum EffectTargetType
{
    Device = 1,
    Group = 2,
}

public class Effect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public EffectAction Action { get; set; } = EffectAction.PulseSwitch;
    public int DurationSeconds { get; set; } = 2;
    public int CooldownSeconds { get; set; } = 2;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<EffectTarget> Targets { get; set; } = [];
}

public class EffectTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EffectId { get; set; }
    public Effect Effect { get; set; } = default!;
    public EffectTargetType TargetType { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? GroupId { get; set; }
    public DeviceGroup? Group { get; set; }
    public int? DurationSecondsOverride { get; set; }
    public int SortOrder { get; set; } = 0;
}