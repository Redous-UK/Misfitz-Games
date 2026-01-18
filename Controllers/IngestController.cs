using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class IngestController(RoomBroadcastService broadcaster, IConfiguration config) : ControllerBase
{
    [HttpPost("/ingest/event")]
    public async Task<IActionResult> Ingest([FromBody] IngestEvent evt, CancellationToken ct)
    {
        // Simple connector auth (MVP)
        var expectedKey = config["CONNECTOR_INGEST_KEY"];
        if (!string.IsNullOrWhiteSpace(expectedKey))
        {
            var providedKey = Request.Headers["X-Connector-Key"].ToString();
            if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
                return Unauthorized(new { ok = false, error = "Invalid connector key" });
        }

        // Broadcast raw event (later you’ll route into game engines)
        await broadcaster.BroadcastStateAsync(evt.RoomId, new
        {
            kind = "ingest",
            evt.RoomId,
            evt.Platform,
            evt.ChannelId,
            evt.UserId,
            evt.Username,
            evt.Type,
            evt.Message,
            evt.TsUtc
        }, ct);

        return Ok(new { ok = true });
    }
}