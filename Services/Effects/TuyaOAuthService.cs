using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Misfitz_Games.Services.Effects;

public class TuyaOAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IDataProtector _protector;

    private readonly string _clientId;
    private readonly string _clientSecret;

    public TuyaOAuthService(AppDbContext db, IConfiguration cfg, IDataProtectionProvider dp)
    {
        _db = db;
        _cfg = cfg;
        _protector = dp.CreateProtector("Misfitz_Games.TuyaOAuthService.v1");

        _clientId = _cfg["TUYA_CLIENT_ID"] ?? throw new InvalidOperationException("TUYA_CLIENT_ID missing");
        _clientSecret = _cfg["TUYA_CLIENT_SECRET"] ?? throw new InvalidOperationException("TUYA_CLIENT_SECRET missing");
    }

    // This is the H5 URL you redirect the user to.
    // Tuya’s docs describe configuring OAuth2 in the Cloud Project and using the configured page/callback. :contentReference[oaicite:3]{index=3}
    // Best practice: put the exact URL in env once you copy it from Tuya console.
    public string BuildAuthorizeUrl(string state)
    {
        var url = _cfg["TUYA_OAUTH_AUTHORIZE_URL"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("TUYA_OAUTH_AUTHORIZE_URL missing. Put the Tuya OAuth H5 URL here.");

        // If your Tuya H5 URL already includes params, we just append state.
        // (Different Tuya configs output slightly different base URLs.)
        var join = url.Contains('?') ? "&" : "?";
        return $"{url}{join}state={Uri.EscapeDataString(state)}";
    }

    public async Task<TuyaAccountLink> ExchangeCodeAndUpsertAsync(Guid userId, string code, string apiBase, CancellationToken ct)
    {
        // Tuya OAuth code exchange uses:
        // GET /v1.0/token?grant_type=2&code=... :contentReference[oaicite:4]{index=4}
        using var http = new HttpClient();

        var path = $"/v1.0/token?grant_type=2&code={Uri.EscapeDataString(code)}";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, apiBase + path);
        SignRequest(req, method, path, body: "", accessToken: null);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("result");

        var tuyaUid = result.GetProperty("uid").GetString() ?? throw new InvalidOperationException("Tuya uid missing");
        var access = result.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("access_token missing");
        var refresh = result.GetProperty("refresh_token").GetString() ?? throw new InvalidOperationException("refresh_token missing");

        var expireSeconds =
            result.TryGetProperty("expire_time", out var et) ? et.GetInt32()
            : result.TryGetProperty("expire", out var e2) ? e2.GetInt32()
            : 7200;

        var expiresUtc = DateTimeOffset.UtcNow.AddSeconds(expireSeconds);

        var row = await _db.TuyaLinks.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (row is null)
        {
            row = new TuyaAccountLink
            {
                UserId = userId,
                TuyaUid = tuyaUid,
                ApiBase = apiBase,
                AccessTokenEnc = _protector.Protect(access),
                RefreshTokenEnc = _protector.Protect(refresh),
                AccessTokenExpiresUtc = expiresUtc,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            _db.TuyaLinks.Add(row);
        }
        else
        {
            row.TuyaUid = tuyaUid;
            row.ApiBase = apiBase;
            row.AccessTokenEnc = _protector.Protect(access);
            row.RefreshTokenEnc = _protector.Protect(refresh);
            row.AccessTokenExpiresUtc = expiresUtc;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<(string tuyaUid, string apiBase, string accessToken)> GetValidAccessTokenAsync(Guid userId, CancellationToken ct)
    {
        var link = await _db.TuyaLinks.SingleOrDefaultAsync(x => x.UserId == userId, ct)
                   ?? throw new InvalidOperationException("Tuya not linked");

        var access = _protector.Unprotect(link.AccessTokenEnc);

        // If you want refresh support later:
        // Tuya provides refresh via GET /v1.0/token/{refresh_token}. :contentReference[oaicite:5]{index=5}
        // For now, keep it simple: if expired, force relink.
        if (DateTimeOffset.UtcNow >= link.AccessTokenExpiresUtc)
            throw new InvalidOperationException("Tuya access token expired. Please relink.");

        return (link.TuyaUid, link.ApiBase, access);
    }

    public async Task<JsonElement> GetUserDevicesAsync(Guid userId, CancellationToken ct)
    {
        // Device list endpoint:
        // GET /v1.0/users/{uid}/devices :contentReference[oaicite:6]{index=6}
        var (uid, apiBase, accessToken) = await GetValidAccessTokenAsync(userId, ct);

        using var http = new HttpClient();
        var path = $"/v1.0/users/{Uri.EscapeDataString(uid)}/devices";
        var method = HttpMethod.Get;

        using var req = new HttpRequestMessage(method, apiBase + path);
        SignRequest(req, method, path, body: "", accessToken: accessToken);

        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res, json);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result").Clone();
    }

    public async Task UnlinkAsync(Guid userId, CancellationToken ct)
    {
        var link = await _db.TuyaLinks.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (link is null) return;

        _db.TuyaLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    // ===== signing helpers (same idea as your TuyaPlugService) =====

    private void SignRequest(HttpRequestMessage req, HttpMethod method, string path, string body, string? accessToken)
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString("N");

        var contentSha256 = Sha256Hex(body ?? "");
        var stringToSign = $"{method.Method}\n{contentSha256}\n\n{path}";
        var signStr = _clientId + (accessToken ?? "") + t + nonce + stringToSign;

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
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string HmacSha256Hex(string key, string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

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