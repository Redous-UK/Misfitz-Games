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
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public DeviceProvider Provider { get; set; } = DeviceProvider.Tuya;
    public DeviceCapability Capability { get; set; } = DeviceCapability.Switch;
    public string ExternalDeviceId { get; set; } = "";
    public string? ExternalSwitchCode { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxPulseSeconds { get; set; } = 30;
    public int CooldownSeconds { get; set; } = 2;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}