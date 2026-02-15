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
        try
        {
            await effects.PulsePlugAsync(req.DeviceName, req.Seconds);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            // Render logs
            Console.WriteLine(ex);

            // Return JSON so the client can display it
            return StatusCode(500, new
            {
                ok = false,
                error = ex.Message,
                type = ex.GetType().FullName
                // If you want full details temporarily:
                details = ex.ToString()
            });
        }
    }
}

public record PulseRequest(string DeviceName, int Seconds = 5);
