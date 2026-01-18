using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class TestBroadcastController(RoomBroadcastService broadcaster) : ControllerBase
{
    [HttpPost("/rooms/{roomId:guid}/test/broadcast")]
    public async Task<IActionResult> Broadcast(Guid roomId)
    {
        var payload = new
        {
            roomId,
            message = "Hello from server!",
            utc = DateTimeOffset.UtcNow
        };

        await broadcaster.BroadcastStateAsync(roomId, payload);
        return Ok(new { ok = true });
    }
}