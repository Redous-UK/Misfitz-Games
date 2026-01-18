using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class GamesController(IRoomStateStore store, RoomBroadcastService broadcaster, ContextoEngine contexto) : ControllerBase
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
        await broadcaster.BroadcastStateAsync(roomId, next, ct);

        return Ok(new { ok = true });
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
        await broadcaster.BroadcastStateAsync(roomId, next, ct);

        return Ok(new { ok = true });
    }
}