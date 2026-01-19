using Microsoft.AspNetCore.Mvc;

namespace Misfitz_Games.Controllers;

[ApiController]
public class DebugController(IConfiguration config) : ControllerBase
{
    [HttpGet("/debug/env")]
    public IActionResult Env()
    {
        var redis = config["REDIS_URL"];
        return Ok(new
        {
            hasRedisUrl = !string.IsNullOrWhiteSpace(redis),
            redisScheme = string.IsNullOrWhiteSpace(redis) ? null : new Uri(redis).Scheme
        });
    }
}