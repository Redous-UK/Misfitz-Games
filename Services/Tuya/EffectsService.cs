namespace Misfitz_Games.Services.Tuya;

public class EffectsService(TuyaPlugService tuya)
{
    public async Task PulsePlugAsync(string deviceName, int seconds, CancellationToken ct)
    {
        // Map friendly names to Tuya device IDs (later move to config/DB)
        var deviceId = deviceName switch
        {
            "plug1" => tuya.DeviceId1,
            _ => throw new ArgumentException("Unknown device")
        };

        await tuya.SetSwitchAsync(deviceId, true, ct);
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30)), ct);
        await tuya.SetSwitchAsync(deviceId, false, ct);
    }
}