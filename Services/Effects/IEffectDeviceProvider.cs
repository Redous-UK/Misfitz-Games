using Misfitz_Games.Models.Effects;

namespace Misfitz_Games.Services.Effects;

public interface IEffectDeviceProvider
{
    string ProviderKey { get; }

    Task<IReadOnlyList<EffectDevice>> GetDevicesAsync(CancellationToken ct = default);
    Task TurnOnAsync(string deviceId, CancellationToken ct = default);
    Task TurnOffAsync(string deviceId, CancellationToken ct = default);
    Task SetBrightnessAsync(string deviceId, int brightnessPercent, CancellationToken ct = default);
    Task SetColorAsync(string deviceId, string hexColor, CancellationToken ct = default);
    Task ActivateSceneAsync(string sceneKey, CancellationToken ct = default);
}