using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;
using System.Security.Claims;

namespace Misfitz_Games.Controllers;

[ApiController]
public class IngestController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster,
    IConfiguration config
) : ControllerBase
{
    [HttpPost("/ingest/event")]
    public async Task<IActionResult> Ingest([FromBody] IngestEvent evt, CancellationToken ct)
    {
        var expectedKey = config["CONNECTOR_INGEST_KEY"];
        //    var isAdmin =
        //        User?.IsInRole("admin") == true ||
        //        User?.Claims?.Any(c => c.Type == "role" && c.Value == "admin") == true ||
        //        User?.Claims?.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin") == true;

        var isAdmin =
            User?.IsInRole("admin") == true ||
            User?.Claims?.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin") == true;

        if (!isAdmin && !string.IsNullOrWhiteSpace(expectedKey))
        {
            var providedKey = Request.Headers["X-Connector-Key"].ToString();
            if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
                return Unauthorized(new { ok = false, error = "Invalid connector key" });
        }

        var resolvedRoomId = await store.ResolveRoomIdAsync(evt.RoomId, ct);
        if (resolvedRoomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(resolvedRoomId.Value, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        RoomState next = state;

        if (state.ActiveGame == GameType.Contexto
            && state.GameState is ContextoState prev
            && next.GameState is ContextoState cur)
        {
            var wasEnded = prev.EndedAtUtc is not null;
            var isEnded = cur.EndedAtUtc is not null;

            if (!wasEnded && isEnded)
            {
                // Add the round's scores into the persistent room leaderboard
                await store.AddToLeaderboardAsync(state.RoomId, cur.ScoresByUserId, ct);
            }
        }

        if (!Equals(next, state))
            await store.SaveStateAsync(next, ct);

        await broadcaster.BroadcastStateAsync(resolvedRoomId.Value, RoomStateProjector.ToPublic(next), ct);

        return Ok(new { ok = true });
    }
}