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

public class TuyaPlugService
{
    // ====== FILL THESE IN (move to config later) ======
    private const string ClientId = "rjcfwedasgs7mnsvt9jn";
    private const string ClientSecret = "e6b9e50fe7394359809124d033b92b5e";

    // This is your plug device ID
    private const string DeviceId = "bf5c1ba9d90c5bf0420t6h";

    // EU region for UK in most cases
    private const string ApiBase = "https://openapi.tuyaeu.com";
    // ================================================

    // Friendly access for EffectsService
    public string DeviceId1 => DeviceId;

    // Cache (basic) to avoid re-fetching functions constantly
    private string? _cachedSwitchCode;
    private DateTimeOffset _switchCodeFetchedAt;

    public async Task SetSwitchAsync(string deviceId, bool on, CancellationToken ct = default)
    {
        using var http = new HttpClient();

        var token = await GetAccessTokenAsync(http, ct);
        var switchCode = await GetSwitchCodeAsync(http, token, deviceId, ct);

        await SendSwitchAsync(http, token, deviceId, switchCode, on, ct);
    }

    private static async Task<string> GetAccessTokenAsync(HttpClient http, CancellationToken ct)
    {
        var path = "/v1.0/token?grant_type=1";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, ApiBase + path);
        SignRequest(req, method, path, body: "", accessToken: null);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").GetProperty("access_token").GetString()!;
    }

    private async Task<string> GetSwitchCodeAsync(HttpClient http, string accessToken, string deviceId, CancellationToken ct)
    {
        // Cache switch code for 10 minutes (device functions rarely change)
        if (_cachedSwitchCode is not null && DateTimeOffset.UtcNow - _switchCodeFetchedAt < TimeSpan.FromMinutes(10))
            return _cachedSwitchCode;

        var path = $"/v1.0/iot-03/devices/{deviceId}/functions";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, ApiBase + path);
        SignRequest(req, method, path, body: "", accessToken: accessToken);

        using var res = await http.SendAsync(req, ct);
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

    private static async Task SendSwitchAsync(HttpClient http, string accessToken, string deviceId, string switchCode, bool on, CancellationToken ct)
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
        using var req = new HttpRequestMessage(method, ApiBase + path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        SignRequest(req, method, path, body, accessToken);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False)
            throw new Exception("Tuya returned success=false: " + json);
    }

    private static void SignRequest(HttpRequestMessage req, HttpMethod method, string path, string body, string? accessToken)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");

        var contentSha256 = Sha256Hex(body ?? "");
        var stringToSign = $"{method.Method}\n{contentSha256}\n\n{path}";
        var signStr = ClientId + (accessToken ?? "") + t + nonce + stringToSign;

        var sign = HmacSha256Hex(ClientSecret, signStr).ToUpperInvariant();

        req.Headers.Remove("client_id");
        req.Headers.Remove("t");
        req.Headers.Remove("sign_method");
        req.Headers.Remove("sign");
        req.Headers.Remove("nonce");
        req.Headers.Remove("access_token");

        req.Headers.Add("client_id", ClientId);
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