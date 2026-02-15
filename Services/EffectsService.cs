namespace Misfitz_Games.Services;

public class EffectsService(TuyaPlugService tuya)
{
    public async Task PulsePlugAsync(string deviceName, int seconds)
    {
        // Map friendly names to Tuya device IDs (later move to config/DB)
        var deviceId = deviceName switch
        {
            "plug1" => tuya.DeviceId1,
            _ => throw new ArgumentException("Unknown device")
        };

        await tuya.SetSwitchAsync(deviceId, true);
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 30)));
        await tuya.SetSwitchAsync(deviceId, false);
    }
}