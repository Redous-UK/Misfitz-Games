using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Misfitz_Games.Models.Effects;

namespace Misfitz_Games.Services.Effects;

public sealed class HueProvider : IEffectDeviceProvider
{
    private readonly HttpClient _http;
    private readonly HueOptions _opts;
    private readonly ILogger<HueProvider> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ProviderKey => "hue";

    public HueProvider(HttpClient http, IOptions<HueOptions> opts, ILogger<HueProvider> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;

        if (string.IsNullOrWhiteSpace(_opts.BridgeIp))
            throw new InvalidOperationException("Hue:BridgeIp is missing.");

        if (string.IsNullOrWhiteSpace(_opts.ApplicationKey))
            throw new InvalidOperationException("Hue:ApplicationKey is missing.");

        _http.BaseAddress = new Uri($"https://{_opts.BridgeIp}/clip/v2/");
        _http.DefaultRequestHeaders.Add("hue-application-key", _opts.ApplicationKey);
    }

    public async Task<IReadOnlyList<EffectDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "resource/light");
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Hue lights fetch failed: {(int)res.StatusCode} {body}");

        var parsed = JsonSerializer.Deserialize<HueListResponse<HueLightResource>>(body, JsonOpts);
        var devices = parsed?.Data?.Select(x => new EffectDevice
        {
            Id = x.Id,
            Name = x.Metadata?.Name ?? x.Id,
            Provider = ProviderKey,
            IsOnline = true
        }).ToList() ?? [];

        return devices;
    }

    public Task TurnOnAsync(string deviceId, CancellationToken ct = default)
        => PatchLightAsync(deviceId, """
        { "on": { "on": true } }
        """, ct);

    public Task TurnOffAsync(string deviceId, CancellationToken ct = default)
        => PatchLightAsync(deviceId, """
        { "on": { "on": false } }
        """, ct);

    public Task SetBrightnessAsync(string deviceId, int brightnessPercent, CancellationToken ct = default)
    {
        brightnessPercent = Math.Clamp(brightnessPercent, 1, 100);
        var json = $$"""
        { "dimming": { "brightness": {{brightnessPercent}} } }
        """;
        return PatchLightAsync(deviceId, json, ct);
    }

    public Task SetColorAsync(string deviceId, string hexColor, CancellationToken ct = default)
    {
        var (r, g, b) = HexToRgb(hexColor);
        var (x, y) = RgbToXy(r, g, b);

        var json = $$"""
        {
          "color": {
            "xy": {
              "x": {{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
              "y": {{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
            }
          },
          "on": { "on": true }
        }
        """;

        return PatchLightAsync(deviceId, json, ct);
    }

    public async Task ActivateSceneAsync(string sceneKey, CancellationToken ct = default)
    {
        switch (sceneKey.ToLowerInvariant())
        {
            case "all_off":
                var lights = await GetDevicesAsync(ct);
                foreach (var light in lights)
                    await TurnOffAsync(light.Id, ct);
                break;

            case "battle_mode":
                await RunBattleModeAsync(ct);
                break;

            case "victory_flash":
                await RunVictoryFlashAsync(ct);
                break;

            case "defeat_fade":
                await RunDefeatFadeAsync(ct);
                break;

            case "hype_mode":
                await RunHypeModeAsync(ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown Hue scene: {sceneKey}");
        }
    }

    private async Task PatchLightAsync(string deviceId, string json, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"resource/light/{deviceId}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Hue update failed for {deviceId}: {(int)res.StatusCode} {body}");
    }

    private async Task RunBattleModeAsync(CancellationToken ct)
    {
        var lights = await GetDevicesAsync(ct);
        foreach (var light in lights)
        {
            await SetColorAsync(light.Id, "#ff0000", ct);
            await SetBrightnessAsync(light.Id, 100, ct);
        }
    }

    private async Task RunVictoryFlashAsync(CancellationToken ct)
    {
        var lights = await GetDevicesAsync(ct);

        for (var i = 0; i < 3; i++)
        {
            foreach (var light in lights)
            {
                await SetColorAsync(light.Id, "#00ff00", ct);
                await SetBrightnessAsync(light.Id, 100, ct);
            }

            await Task.Delay(200, ct);

            foreach (var light in lights)
                await TurnOffAsync(light.Id, ct);

            await Task.Delay(150, ct);
        }
    }

    private async Task RunDefeatFadeAsync(CancellationToken ct)
    {
        var lights = await GetDevicesAsync(ct);

        foreach (var light in lights)
            await SetColorAsync(light.Id, "#990000", ct);

        foreach (var level in new[] { 100, 70, 40, 15, 1 })
        {
            foreach (var light in lights)
                await SetBrightnessAsync(light.Id, level, ct);

            await Task.Delay(300, ct);
        }
    }

    private async Task RunHypeModeAsync(CancellationToken ct)
    {
        var lights = await GetDevicesAsync(ct);
        var colors = new[] { "#ff00ff", "#00ffff", "#ffff00", "#ff6600" };

        for (var i = 0; i < 8; i++)
        {
            var color = colors[i % colors.Length];
            foreach (var light in lights)
            {
                await SetColorAsync(light.Id, color, ct);
                await SetBrightnessAsync(light.Id, 100, ct);
            }

            await Task.Delay(180, ct);
        }
    }

    private static (int R, int G, int B) HexToRgb(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) throw new ArgumentException("Expected 6-digit hex color.", nameof(hex));

        return (
            Convert.ToInt32(hex[..2], 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16)
        );
    }

    // Simple approximation, good enough for v1
    private static (double X, double Y) RgbToXy(int r, int g, int b)
    {
        double R = Pivot(r / 255.0);
        double G = Pivot(g / 255.0);
        double B = Pivot(b / 255.0);

        var X = R * 0.664511 + G * 0.154324 + B * 0.162028;
        var Y = R * 0.283881 + G * 0.668433 + B * 0.047685;
        var Z = R * 0.000088 + G * 0.072310 + B * 0.986039;

        var sum = X + Y + Z;
        if (sum <= 0.0) return (0.0, 0.0);

        return (X / sum, Y / sum);

        static double Pivot(double c) =>
            c > 0.04045 ? Math.Pow((c + 0.055) / 1.055, 2.4) : c / 12.92;
    }

    private sealed class HueListResponse<T>
    {
        public List<T>? Data { get; set; }
    }

    private sealed class HueLightResource
    {
        public string Id { get; set; } = "";
        public HueMetadata? Metadata { get; set; }
    }

    private sealed class HueMetadata
    {
        public string? Name { get; set; }
    }
}