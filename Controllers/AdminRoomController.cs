using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class AdminRoomsController(IRoomStateStore store, RoomBroadcastService broadcaster) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/rooms/{roomId:guid}/close")]
    public async Task<IActionResult> Close(Guid roomId, CancellationToken ct)
    {
        // Broadcast a last “closed” notification if you want
        // (clients should treat this as “disconnect / go back to lobby”)
        await broadcaster.BroadcastRoomClosedAsync(roomId, ct);

        var removed = await store.DeleteRoomAsync(roomId, ct);
        return Ok(new { ok = true, roomId, removed });
    }
}