using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services.Effects;

namespace Misfitz_Games.Controllers.Effects;

[ApiController]
[Route("api/hue")]
public sealed class HueDebugController(HueProvider hue, EffectsEngine engine) : ControllerBase
{
    private readonly HueProvider _hue = hue;
    private readonly EffectsEngine _engine = engine;

    [HttpGet("lights")]
    public async Task<IActionResult> GetLights(CancellationToken ct)
        => Ok(await _hue.GetDevicesAsync(ct));

    [HttpPost("scene/{sceneKey}")]
    public async Task<IActionResult> RunScene(string sceneKey, CancellationToken ct)
    {
        await _engine.RunSceneAsync(sceneKey, ct);
        return Ok(new { ok = true, sceneKey });
    }
}