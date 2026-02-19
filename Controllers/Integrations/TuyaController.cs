using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services.Tuya;
using System.Security.Claims;

namespace Misfitz_Games.Controllers.Integrations;

[ApiController]
[Route("api/tuya")]
[Authorize(Policy = "Player")]
public class TuyaController(TuyaOAuthService tuya) : ControllerBase
{
    private Guid GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException("Missing/invalid NameIdentifier (expected GUID).");
    }

    // 1) Start link: returns the URL the frontend should navigate to
    [HttpGet("link/start")]
    public IActionResult StartLink()
    {
        // state prevents CSRF. You can store this in DB if you want; for now encode user id.
        var state = GetUserId().ToString("N");
        var url = tuya.BuildAuthorizeUrl(state);
        return Ok(new { ok = true, url });
    }

    // 2) Callback: Tuya redirects here with ?code=...&state=...
    // You must set this exact URL in Tuya console “callback URL”. :contentReference[oaicite:7]{index=7}
    [AllowAnonymous]
    [HttpGet("link/callback")]
    public async Task<IActionResult> LinkCallback([FromQuery] string code, [FromQuery] string? state, CancellationToken ct)
    {
        // You can require the user to be logged in instead if you prefer.
        // Here we accept state containing the Misfitz user id for simplicity.
        if (string.IsNullOrWhiteSpace(state) || state.Length < 16)
            return BadRequest(new { ok = false, error = "Missing state" });

        // state is GUID without dashes
        if (!Guid.TryParseExact(state, "N", out var userId))
            return BadRequest(new { ok = false, error = "Invalid state" });

        // IMPORTANT:
        // Tuya says the code is DC-specific; store api base for the correct DC. :contentReference[oaicite:8]{index=8}
        // For now choose from env (EU default). If later you support multi-DC, you can infer from callback host or config.
        var apiBase = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["TUYA_API_BASE"]
                      ?? "https://openapi.tuyaeu.com";

        await tuya.ExchangeCodeAndUpsertAsync(userId, code, apiBase, ct);

        // Redirect back into the app UI
        return Redirect("/member.html#tuya=linked");
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        try
        {
            var (uid, apiBase, _) = await tuya.GetValidAccessTokenAsync(GetUserId(), ct);
            return Ok(new { ok = true, linked = true, uid, apiBase });
        }
        catch
        {
            return Ok(new { ok = true, linked = false });
        }
    }

    [HttpPost("unlink")]
    public async Task<IActionResult> Unlink(CancellationToken ct)
    {
        await tuya.UnlinkAsync(GetUserId(), ct);
        return Ok(new { ok = true });
    }

    // Quick test: fetch device list directly from Tuya for this linked user
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken ct)
    {
        var devices = await tuya.GetUserDevicesAsync(GetUserId(), ct);
        return Ok(new { ok = true, devices });
    }


}