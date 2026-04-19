using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Admin;

[ApiController]
public class AdminRoomsController(IRoomStateStore store, RoomBroadcastService broadcaster) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/admin/rooms/{roomRef}/close")]
    public async Task<IActionResult> CloseByRef(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
        {
            return NotFound(new { ok = false, error = "Room not found." });
        }

        await broadcaster.BroadcastRoomClosedAsync(roomId.Value, ct);
        var removed = await store.DeleteRoomAsync(roomId.Value, ct);

        return Ok(new
        {
            ok = true,
            roomRef = roomRef.Trim().ToUpperInvariant(),
            roomId = roomId.Value,
            removed
        });
    }
}