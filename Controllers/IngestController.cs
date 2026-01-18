using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class IngestController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster,
    ContextoEngine contexto,
    IConfiguration config
) : ControllerBase
{
    [HttpPost("/ingest/event")]
    public async Task<IActionResult> Ingest([FromBody] IngestEvent evt, CancellationToken ct)
    {
        // Connector auth (MVP)
        var expectedKey = config["CONNECTOR_INGEST_KEY"];
        if (!string.IsNullOrWhiteSpace(expectedKey))
        {
            var providedKey = Request.Headers["X-Connector-Key"].ToString();
            if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
                return Unauthorized(new { ok = false, error = "Invalid connector key" });
        }

        var state = await store.GetStateAsync(evt.RoomId, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        RoomState next = state;

        // Route to game
        if (state.ActiveGame == GameType.Contexto && state.GameState is not null)
        {
            if (contexto.TryExtractGuess(evt.Message, out var guess))
                next = contexto.ApplyGuess(state, evt.UserId, evt.Username, guess);
        }

        // (Always broadcast something so the overlay can show activity if you want)
        // For now we broadcast updated state (or same state if no change).
        if (!ReferenceEquals(next, state))
            await store.SaveStateAsync(next, ct);

        await broadcaster.BroadcastStateAsync(evt.RoomId, next, ct);

        return Ok(new { ok = true });
    }
}