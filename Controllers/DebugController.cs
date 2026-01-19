using Microsoft.AspNetCore.Mvc;

namespace Misfitz_Games.Controllers;

[ApiController]
public class DebugController(IConfiguration config) : ControllerBase
{
    [HttpGet("/debug/env")]
    public IActionResult Env()
    {
        var redis = config["REDIS_URL"];
        var u = new Uri(redis);
        return Ok(new { hasRedisUrl = true, redisScheme = u.Scheme, host = u.Host, port = u.Port });

    }
}