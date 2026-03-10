using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services.Room;
using System.Security.Claims;

namespace Misfitz_Games.Controllers.Rooms;

[ApiController]
public class WebPresenceController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster
) : ControllerBase
{
    [Authorize] // cookie auth required
    [HttpPost("/rooms/{roomRef}/presence")]
    public async Task<IActionResult> TouchPresence([FromRoute] string roomRef, CancellationToken ct)
    {
        // Be tolerant to claim type differences
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("userId") ??
            User.FindFirstValue("id");

        var username =
            User.FindFirstValue(ClaimTypes.Name) ??
            User.FindFirstValue("name") ??
            User.FindFirstValue("username");

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { ok = false, error = "Invalid auth session" });

        var resolvedRoomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (resolvedRoomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(resolvedRoomId.Value, ct);
        if (state is null)
            return NotFound(new { ok = false, error = "Room state not found" });

        var next = RoomPresenceUpdater.TouchPlayer(state, userId, username, isConnected: true);

        if (!Equals(next, state))
            await store.SaveStateAsync(next, ct);

        await broadcaster.BroadcastStateAsync(resolvedRoomId.Value, RoomStateProjector.ToPublic(next), ct);

        return Ok(new { ok = true });
    }
}
