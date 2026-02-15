using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Misfitz_Games.Services;

public class TuyaPlugService(IConfiguration cfg, HttpClient http)
{
    private readonly string _clientId = cfg["TUYA_CLIENT_ID"] ?? throw new InvalidOperationException("TUYA_CLIENT_ID missing");
    private readonly string _clientSecret = cfg["TUYA_CLIENT_SECRET"] ?? throw new InvalidOperationException("TUYA_CLIENT_SECRET missing");
    private readonly string _apiBase = cfg["TUYA_API_BASE"] ?? "https://openapi.tuyaeu.com";
    private readonly string _defaultDeviceId = cfg["TUYA_DEFAULT_DEVICE_ID"] ?? "";
    private readonly HttpClient _http = http;

    public string DeviceId1 =>
        !string.IsNullOrWhiteSpace(_defaultDeviceId)
            ? _defaultDeviceId
            : throw new InvalidOperationException("TUYA_DEFAULT_DEVICE_ID missing");

    // Cache (basic) to avoid re-fetching functions constantly
    private string? _cachedSwitchCode;
    private DateTimeOffset _switchCodeFetchedAt;

    public async Task SetSwitchAsync(string deviceId, bool on, CancellationToken ct = default)
    {

        var token = await GetAccessTokenAsync(ct);
        var switchCode = await GetSwitchCodeAsync(token, deviceId, ct);

        await SendSwitchAsync(token, deviceId, switchCode, on, ct);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var path = "/v1.0/token?grant_type=1";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, _apiBase + path);
        SignRequest(req, method, path, body: "", accessToken: null);

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("access_token").GetString()!;
    }

    private async Task<string> GetSwitchCodeAsync(string accessToken, string deviceId, CancellationToken ct)
    {
        // Cache switch code for 10 minutes (device functions rarely change)
        if (_cachedSwitchCode is not null && DateTimeOffset.UtcNow - _switchCodeFetchedAt < TimeSpan.FromMinutes(10))
            return _cachedSwitchCode;

        var path = $"/v1.0/iot-03/devices/{deviceId}/functions";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, _apiBase + path);
        SignRequest(req, method, path, body: "", accessToken: accessToken);

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);

        var result = doc.RootElement.GetProperty("result");

        JsonElement functionsEl;

        // Tuya commonly returns: result: { functions: [ ... ] }
        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("functions", out var fns))
        {
            functionsEl = fns;
        }
        // Some APIs may return: result: [ ... ]
        else if (result.ValueKind == JsonValueKind.Array)
        {
            functionsEl = result;
        }
        else
        {
            throw new InvalidOperationException($"Unexpected Tuya /functions payload shape. result is {result.ValueKind}.");
        }

        var funcs = functionsEl.EnumerateArray()
            .Select(e => new
            {
                Code = e.GetProperty("code").GetString(),
                Type = e.TryGetProperty("type", out var t) ? t.GetString() : null
            })
            .ToList();

        // Prefer switch_1, then any code starting with "switch"
        var best =
            funcs.FirstOrDefault(f => string.Equals(f.Code, "switch_1", StringComparison.OrdinalIgnoreCase)) ??
            funcs.FirstOrDefault(f => f.Code != null && f.Code.StartsWith("switch", StringComparison.OrdinalIgnoreCase));

        if (best?.Code is null)
            throw new InvalidOperationException("Could not find a switch function. Inspect the /functions response for your device.");

        _cachedSwitchCode = best.Code;
        _switchCodeFetchedAt = DateTimeOffset.UtcNow;
        return best.Code;
    }

    private async Task SendSwitchAsync(string accessToken, string deviceId, string switchCode, bool on, CancellationToken ct)
    {
        var path = $"/v1.0/iot-03/devices/{deviceId}/commands";
        var method = HttpMethod.Post;

        var bodyObj = new
        {
            commands = new[]
            {
                new { code = switchCode, value = on }
            }
        };

        var body = JsonSerializer.Serialize(bodyObj);
        using var req = new HttpRequestMessage(method, _apiBase + path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        SignRequest(req, method, path, body, accessToken);

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False)
            throw new Exception("Tuya returned success=false: " + json);
    }

    private void SignRequest(HttpRequestMessage req, HttpMethod method, string path, string body, string? accessToken)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");

        var contentSha256 = Sha256Hex(body ?? "");
        var stringToSign = $"{method.Method}\n{contentSha256}\n\n{path}";
        var signStr = _clientId
                      + (accessToken ?? "")
                      + t
                      + nonce
                      + stringToSign;

        var sign = HmacSha256Hex(_clientSecret, signStr).ToUpperInvariant();

        req.Headers.Remove("client_id");
        req.Headers.Remove("t");
        req.Headers.Remove("sign_method");
        req.Headers.Remove("sign");
        req.Headers.Remove("nonce");
        req.Headers.Remove("access_token");

        req.Headers.Add("client_id", _clientId);
        req.Headers.Add("t", t);
        req.Headers.Add("sign_method", "HMAC-SHA256");
        req.Headers.Add("sign", sign);
        req.Headers.Add("nonce", nonce);

        if (!string.IsNullOrEmpty(accessToken))
            req.Headers.Add("access_token", accessToken);
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return ConvertToHex(bytes);
    }

    private static string HmacSha256Hex(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return ConvertToHex(bytes);
    }

    private static string ConvertToHex(byte[] bytes)
        => string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));

    private static void EnsureSuccess(HttpResponseMessage res, string body)
    {
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\n{body}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False)
                throw new Exception("Tuya API success=false\n" + body);
        }
        catch (JsonException)
        {
            // ignore
        }
    }
}