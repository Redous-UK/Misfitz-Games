namespace Misfitz_Games.Models.Effects;

public sealed class EffectDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public bool IsOnline { get; set; }
}

public sealed class HueOptions
{
    public string BridgeIp { get; set; } = "";
    public string ApplicationKey { get; set; } = "";
    public string? EntertainmentGroupRid { get; set; }
}