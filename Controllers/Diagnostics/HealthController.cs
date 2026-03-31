using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services.Infrastructure.Redis;
using Misfitz_Games.Services.Effects;
using Npgsql;

namespace Misfitz_Games.Controllers.Diagnostics;

[ApiController]
[Route("api/health")]
public class HealthController(
    IConfiguration config,
    RedisMuxFactory muxFactory,
    TuyaPlugService tuyaPlug,
    ILogger<HealthController> log
) : ControllerBase
{
    private readonly IConfiguration _config = config;
    private readonly RedisMuxFactory _muxFactory = muxFactory;
    private readonly TuyaPlugService _tuyaPlug = tuyaPlug;
    private readonly ILogger<HealthController> _log = log;

    // GET /api/health/tuya
    [HttpGet("tuya")]
    public async Task<IActionResult> Tuya()
        => await TuyaHealthCore();

    // GET /healthz  (your existing aggregate check)
    [HttpGet("/healthz")]
    public async Task<IActionResult> Healthz()
    {
        var results = new Dictionary<string, object>();
        var ok = true;

        // --- Redis check ---
        var redisUrl = _config["REDIS_URL"];
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            try
            {
                var mux = await _muxFactory.GetAsync().ConfigureAwait(false);
                var db = mux.GetDatabase();
                var pong = await db.PingAsync();

                results["redis"] = new
                {
                    ok = true,
                    isConnected = mux.IsConnected,
                    pingMs = (int)pong.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                ok = false;
                results["redis"] = new
                {
                    ok = false,
                    error = ex.Message,
                    type = ex.GetType().FullName,
                    inner = ex.InnerException?.Message
                };
            }
        }
        else
        {
            results["redis"] = new { ok = true, skipped = true, reason = "REDIS_URL not set" };
        }

        // --- Postgres check ---
        var databaseUrl = _config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            try
            {
                var connString = ConvertDatabaseUrlToNpgsql(databaseUrl);
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                var scalar = await cmd.ExecuteScalarAsync();

                results["postgres"] = new { ok = true, scalar };
            }
            catch (Exception ex)
            {
                ok = false;
                results["postgres"] = new { ok = false, error = ex.Message };
            }
        }
        else
        {
            results["postgres"] = new { ok = true, skipped = true, reason = "DATABASE_URL not set" };
        }

        // --- Tuya check ---
        try
        {
            await _tuyaPlug.HealthPingAsync();
            results["tuya"] = new { ok = true };
        }
        catch (Exception ex)
        {
            ok = false;
            results["tuya"] = new { ok = false, error = ex.Message };
        }

        results["service"] = new { ok = true, name = "Misfitz-Games", utc = DateTimeOffset.UtcNow };

        return ok ? Ok(results) : StatusCode(503, results);
    }

    // GET /healthz/tuya  (keep this if you already rely on it)
    [HttpGet("/healthz/tuya")]
    public async Task<IActionResult> TuyaHealth()
        => await TuyaHealthCore();

    private async Task<IActionResult> TuyaHealthCore()
    {
        try
        {
            var result = await TuyaPingResult();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tuya health check failed");
            return StatusCode(503, new
            {
                ok = false,
                service = "tuya",
                status = "offline",
                error = ex.Message,
                atUtc = DateTime.UtcNow
            });
        }
    }

    private async Task<object> TuyaPingResult()
    {
        // Use the lightest call you have. Prefer "time" or "token".
        // Replace PingAsync() with your real method name if different.
        await _tuyaPlug.HealthPingAsync();

        return new
        {
            ok = true,
            service = "tuya",
            status = "online",
            atUtc = DateTime.UtcNow
        };
    }

    private static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

        var database = uri.AbsolutePath.Trim('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port == -1 ? 5432 : uri.Port,
            Username = username,
            Password = password,
            Database = database,
            SslMode = SslMode.Require,
        };

        return builder.ConnectionString;
    }
}
