using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
    // Allow both:
    // - TikTok connector (no cookie) using X-Connector-Key
    // - Web users (cookie auth) guest/member/admin
    [AllowAnonymous]
    [HttpPost("/ingest/event")]
    public async Task<IActionResult> Ingest([FromBody] IngestEvent evt, CancellationToken ct)
    {
        var expectedKey = config["CONNECTOR_INGEST_KEY"];
        var providedKey = Request.Headers["X-Connector-Key"].ToString();

        var isCookieAuthed = User?.Identity?.IsAuthenticated ?? false;

        var hasValidConnectorKey =
            !string.IsNullOrWhiteSpace(expectedKey) &&
            !string.IsNullOrWhiteSpace(providedKey) &&
            string.Equals(providedKey, expectedKey, StringComparison.Ordinal);

        // ✅ Require EITHER cookie auth OR a valid connector key
        if (!isCookieAuthed && !hasValidConnectorKey)
            return Unauthorized(new { ok = false, error = "Invalid connector key" });

        // ✅ If this came from the web (cookie-auth) and NOT the connector,
        // force identity from claims (prevents spoofing evt.UserId/evt.Username)
        if (isCookieAuthed && !hasValidConnectorKey)
        {
            var claimUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var claimName = User?.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(claimUserId) || string.IsNullOrWhiteSpace(claimName))
                return Unauthorized(new { ok = false, error = "Invalid auth session" });

            // IngestEvent is likely a record -> use `with`
            // If it's a class, change to: evt.UserId = claimUserId; evt.Username = claimName;
            evt = evt with
            {
                Platform = "web",
                UserId = claimUserId,
                Username = claimName
            };
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

        // ✅ Update presence for ANY event (web or tiktok)
        next = RoomPresenceUpdater.TouchPlayer(next, evt.UserId, evt.Username, isConnected: true);

        // ---- Route to active game ----
        if (evt.Type == "chat" && next.ActiveGame == GameType.Contexto) // use next, not state
        {
            if (contexto.TryExtractGuess(evt.Message, out var guess))
            {
                next = contexto.ApplyGuess(next, evt.UserId, evt.Username, guess); // ✅ use next, not state
            }
        }

        // ---- Persist leaderboard when a Contexto round ends ----
        if (!Equals(next, prevState)
            && prevState.ActiveGame == GameType.Contexto
            && next.ActiveGame == GameType.Contexto)
        {
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
