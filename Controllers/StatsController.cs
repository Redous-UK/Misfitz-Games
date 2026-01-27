using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class StatsController(IRoomStateStore store) : ControllerBase
{
    [HttpGet("/rooms/{roomRef}/stats")]
    public async Task<IActionResult> GetStats(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var stats = await store.GetRoomStatsAsync(roomId.Value, ct);
        return Ok(new { ok = true, stats });
    }
}