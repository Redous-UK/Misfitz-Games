using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class GamesController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster,
    ContextoEngine contexto,
    ContextoWordProvider words
) : ControllerBase
{
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

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomId:guid}/games/contexto/next")]
    public async Task<IActionResult> NextContextoRound(Guid roomId, CancellationToken ct)
    {
        var state = await store.GetStateAsync(roomId, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var secret = words.NextSecret();

        var next = state with
        {
            ActiveGame = GameType.Contexto,
            GameState = ContextoEngine.NewRound(secret),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);
        await broadcaster.BroadcastStateAsync(roomId, RoomStateProjector.ToPublic(next), ct);

        return Ok(new { ok = true });
    }

    [HttpGet("/rooms/{roomId:guid}/leaderboard")]
    public async Task<IActionResult> Leaderboard(Guid roomId, CancellationToken ct)
    {
        var state = await store.GetStateAsync(roomId, ct);
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

    [HttpPost("/rooms/{roomId:guid}/games/stop")]
    public async Task<IActionResult> Stop(Guid roomId, CancellationToken ct)
    {
        var state = await store.GetStateAsync(roomId, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var next = state with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);
        await broadcaster.BroadcastStateAsync(roomId, RoomStateProjector.ToPublic(next), ct);

        return Ok(new { ok = true });
    }
}