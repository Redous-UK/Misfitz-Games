using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Contexto;
using Misfitz_Games.Services.Room;
using System.Security.Claims;

namespace Misfitz_Games.Controllers;

[ApiController]
public class GamesController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster,
    ContextoWordProvider words,
    ContextoEngine contexto
) : ControllerBase
{

    public record GuessReq(string Guess);

    // ✅ Anyone allowed to play (guest/member/admin) via cookie auth
    [Authorize(Policy = "Player")]
    [HttpPost("/rooms/{roomRef}/games/contexto/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] GuessReq req, CancellationToken ct)
    {
        var guess = (req?.Guess ?? "").Trim();
        if (guess.Length < 1 || guess.Length > 64)
            return BadRequest(new { ok = false, error = "Guess is required (1-64 chars)." });

        // ✅ cookie-auth identity
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Player";

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, error = "Not authenticated." });

        // Resolve room
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
            return NotFound(new { ok = false, error = "Room not found." });

        // Load state
        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null)
            return NotFound(new { ok = false, error = "State not found." });

        // Ensure Contexto is active
        if (state.ActiveGame != GameType.Contexto)
            return BadRequest(new { ok = false, error = "Contexto is not active in this room." });

        // ✅ Apply guess using your engine
        var updated = contexto.ApplyGuess(state, userId, username, guess);

        // If engine ignored it (inactive / empty), still return ok
        await store.SaveStateAsync(updated, ct);
        await broadcaster.BroadcastStateAsync(roomId.Value, updated, ct);

        // ✅ Return the newest guess details so UI can update instantly (no extra /state fetch needed)
        // This is safe even if GameState is stored as JsonElement: it’s already materialized inside ApplyGuess.
        var cs = updated.GameState as ContextoState;
        var latest = cs?.RecentGuesses?.FirstOrDefault();


        if (latest is null)
            return Ok(new { ok = true });


        return Ok(new
        {
            ok = true,
            guess = new
            {
                latest.Guess,
                latest.Percentage,
                latest.RankOrScore,
                latest.IsWinner,
                latest.TsUtc
            },
            game = new
            {
                isActive = cs!.IsActive,
                endedAtUtc = cs.EndedAtUtc
            }
        });
    }

    [HttpPost("/rooms/{roomId:guid}/games/contexto/start")]
    public async Task<IActionResult> StartContexto(Guid roomId, [FromBody] ContextoStartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SecretWord))
            return BadRequest(new { ok = false, error = "SecretWord is required" });

        var state = await store.GetStateAsync(roomId, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var next = state with
        {
            ActiveGame = GameType.Contexto,
            GameState = ContextoEngine.NewRound(req.SecretWord),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);
        await broadcaster.BroadcastStateAsync(roomId, RoomStateProjector.ToPublic(next), ct);
        await store.IncrementGamesPlayedAsync(state.RoomId, 1, ct);

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomRef}/games/contexto/next")]
    public async Task<IActionResult> NextContextoRound(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var secret = words.NextSecret();

        var next = state with
        {
            ActiveGame = GameType.Contexto,
            GameState = ContextoEngine.NewRound(secret),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);
        await broadcaster.BroadcastStateAsync(roomId.Value, RoomStateProjector.ToPublic(next), ct);
        await store.IncrementGamesPlayedAsync(state.RoomId, 1, ct);

        return Ok(new { ok = true });
    }

    [HttpGet("/rooms/{roomRef}/leaderboard")]
    public async Task<IActionResult> Leaderboard(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        if (state.ActiveGame == GameType.Contexto && state.GameState is ContextoState cs)
        {
            var top = cs.ScoresByUserId
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new { userId = kv.Key, score = kv.Value })
                .ToList();

            return Ok(new { ok = true, roomId, game = "contexto", top });
        }

        return Ok(new { ok = true, roomId, game = state.ActiveGame.ToString(), top = Array.Empty<object>() });
    }

    [HttpPost("/rooms/{roomRef}/games/stop")]
    public async Task<IActionResult> Stop(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var next = state with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);
        await broadcaster.BroadcastStateAsync(roomId.Value, RoomStateProjector.ToPublic(next), ct);

        return Ok(new { ok = true });
    }
}