using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;
using Misfitz_Games.Services.Effects;
using Misfitz_Games.Controllers;
using System.Text.Json;



namespace Misfitz_Games.Controllers.Integrations;

[ApiController]
[Route("api/hooks")]
public class HooksController(IConfiguration cfg, WebhookIngestService ingest) : ControllerBase
{
    private string Secret => cfg["HOOK_SECRET"] ?? throw new InvalidOperationException("HOOK_SECRET missing");

    // TikFinity -> Misfitz
    // Configure TikFinity webhook to POST JSON here with header: X-Misfitz-Secret: <HOOK_SECRET>
    [HttpPost("tiktok")]
    public async Task<IActionResult> TikTok([FromBody] JsonElement payload, CancellationToken ct)
        => await Handle("tikfinity", payload, ct);

    // Streamer.bot -> Misfitz
    // Configure Streamer.bot HTTP Request action to POST JSON here with header: X-Misfitz-Secret: <HOOK_SECRET>
    [HttpPost("streamerbot")]
    public async Task<IActionResult> StreamerBot([FromBody] JsonElement payload, CancellationToken ct)
        => await Handle("streamerbot", payload, ct);

    private async Task<IActionResult> Handle(string source, JsonElement payload, CancellationToken ct)
    {
        // Basic shared-secret auth
        var got = Request.Headers["X-Misfitz-Secret"].ToString();
        if (!string.Equals(got, Secret, StringComparison.Ordinal))
            return Unauthorized(new { ok = false, error = "Bad secret" });

        var result = await ingest.ProcessAsync(source, payload, ct);
        return Ok(new { ok = true, handled = result.Handled, message = result.Message });
    }
}