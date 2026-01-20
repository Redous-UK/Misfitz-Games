using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;
using Npgsql;
using StackExchange.Redis;

namespace Misfitz_Games.Controllers;

[ApiController]
public class HealthController(IConfiguration config, RedisMuxFactory muxFactory) : ControllerBase
{
    [HttpGet("/healthz")]
    public async Task<IActionResult> Healthz()
    {
        var results = new Dictionary<string, object>();
        var ok = true;

        // --- Redis check ---
        var redisUrl = config["REDIS_URL"];
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            try
            {
                var mux = await muxFactory.GetAsync().ConfigureAwait(false);
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
        var databaseUrl = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            try
            {
                var connString = ConvertDatabaseUrlToNpgsql(databaseUrl);
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                var scalar = await cmd.ExecuteScalarAsync();

                results["postgres"] = new
                {
                    ok = true,
                    scalar
                };
            }
            catch (Exception ex)
            {
                ok = false;
                results["postgres"] = new
                {
                    ok = false,
                    error = ex.Message
                };
            }
        }
        else
        {
            results["postgres"] = new { ok = true, skipped = true, reason = "DATABASE_URL not set" };
        }

        results["service"] = new
        {
            ok = true,
            name = "Misfitz-Games",
            utc = DateTimeOffset.UtcNow
        };

        return ok ? Ok(results) : StatusCode(503, results);
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