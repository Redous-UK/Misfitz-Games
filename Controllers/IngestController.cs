using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;
using System.Security.Claims;


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
        // Connector auth (bypass for admin cookie)
        var expectedKey = config["CONNECTOR_INGEST_KEY"];


        var isAdmin =
        User?.IsInRole("admin") == true ||
        User?.Claims?.Any(c => c.Type == ClaimTypes.Role && c.Value == "admin") == true ||
        User?.Claims?.Any(c => c.Type == "role" && c.Value == "admin") == true;


        if (!isAdmin && !string.IsNullOrWhiteSpace(expectedKey))
        {
            var providedKey = Request.Headers["X-Connector-Key"].ToString();
            if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
                return Unauthorized(new { ok = false, error = "Invalid connector key" });
        }


        // Resolve room (accept GUID or room code)
        var resolvedRoomId = await store.ResolveRoomIdAsync(evt.RoomId, ct);
        if (resolvedRoomId is null)
            return NotFound(new { ok = false, error = "Room not found" });


        var state = await store.GetStateAsync(resolvedRoomId.Value, ct);
        if (state is null)
            return NotFound(new { ok = false, error = "Room state not found" });


        var prevState = state;
        RoomState next = state;


        // ---- Route to active game ----
        if (state.ActiveGame == GameType.Contexto)
        {
            if (contexto.TryExtractGuess(evt.Message, out var guess))
            {
                next = contexto.ApplyGuess(state, evt.UserId, evt.Username, guess);
            }
        }


        // ---- Persist leaderboard when a Contexto round ends ----
        // (Detect transition: not ended -> ended)
        if (!Equals(next, prevState)
        && prevState.ActiveGame == GameType.Contexto
        && next.ActiveGame == GameType.Contexto)
        {
            // Prefer engine-safe state extraction: ApplyGuess should set GameState to ContextoState
            if (prevState.GameState is ContextoState prev
            && next.GameState is ContextoState cur)
            {
                var wasEnded = prev.EndedAtUtc is not null;
                var isEnded = cur.EndedAtUtc is not null;


                if (!wasEnded && isEnded)
                {
                    await store.AddToLeaderboardAsync(prevState.RoomId, cur.ScoresByUserId, ct);
                }
            }
        }


        // Save only if changed
        if (!Equals(next, prevState))
            await store.SaveStateAsync(next, ct);


        await broadcaster.BroadcastStateAsync(resolvedRoomId.Value, RoomStateProjector.ToPublic(next), ct);


        return Ok(new { ok = true });
    }
}