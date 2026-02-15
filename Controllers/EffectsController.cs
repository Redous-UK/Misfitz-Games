using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
[Route("api/effects")]
public class EffectsController(EffectsService effects) : ControllerBase
{
    [HttpPost("plug/pulse")]
    public async Task<IActionResult> PulsePlug([FromBody] PulseRequest req)
    {
        await effects.PulsePlugAsync(req.DeviceName, req.Seconds);
        return Ok(new { ok = true });
    }
}

public record PulseRequest(string DeviceName, int Seconds = 5);
