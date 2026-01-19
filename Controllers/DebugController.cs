using Microsoft.AspNetCore.Mvc;

namespace Misfitz_Games.Controllers;

[ApiController]
public class DebugController(IConfiguration config) : ControllerBase
{
    [HttpGet("/debug/env")]
    public IActionResult Env()
    {
        var redis = config["REDIS_URL"];
        if (string.IsNullOrWhiteSpace(redis))
            return Ok(new { hasRedisUrl = false });

        var u = new Uri(redis);
        return Ok(new
        {
            hasRedisUrl = true,
            redisScheme = u.Scheme,
            host = u.Host,
            port = u.Port,
            hasUserInfo = !string.IsNullOrWhiteSpace(u.UserInfo)
        });
    }
}