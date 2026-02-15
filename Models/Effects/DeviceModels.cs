namespace Misfitz_Games.Models.Effects;

public enum DeviceProvider
{
    Tuya = 1,
}

public enum DeviceCapability
{
    Switch = 1,
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Owner (AppDbContext User.Id)
    public int OwnerUserId { get; set; }

    // Friendly name (unique per user)
    public string Name { get; set; } = "";

    public DeviceProvider Provider { get; set; } = DeviceProvider.Tuya;
    public DeviceCapability Capability { get; set; } = DeviceCapability.Switch;

    // Provider-specific identifier (Tuya DeviceId)
    public string ExternalDeviceId { get; set; } = "";

    // Optional override for Tuya function code (e.g. "switch_1")
    public string? ExternalSwitchCode { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Safety defaults
    public int MaxPulseSeconds { get; set; } = 30;
    public int CooldownSeconds { get; set; } = 2;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}