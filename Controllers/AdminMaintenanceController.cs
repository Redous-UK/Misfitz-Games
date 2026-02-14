using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers;

[ApiController]
public class AdminMaintenanceController(IRoomStateStore store) : ControllerBase
{

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/admin/rooms/cleanup/preview")]
    public async Task<IActionResult> Preview([FromQuery] int olderThanHours = 24, [FromQuery] int max = 200, CancellationToken ct = default)
    {
        if (olderThanHours < 1) olderThanHours = 1;
        if (max < 1) max = 1;
        if (max > 2000) max = 2000;

        var cutoffUtc = DateTimeOffset.UtcNow.AddHours(-olderThanHours);
        var rooms = await store.ListRoomsOlderThanAsync(cutoffUtc, max, ct);

        return Ok(new
        {
            ok = true,
            olderThanHours,
            max,
            cutoffUtc,
            count = rooms.Count,
            rooms = rooms.Select(r => new { r.RoomId, r.Name, r.CreatedAtUtc }).ToArray()
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/admin/rooms/cleanup")]
    public async Task<IActionResult> CleanupRooms([FromQuery] int olderThanHours = 24, [FromQuery] int max = 200, CancellationToken ct = default)
    {
        if (olderThanHours < 1) olderThanHours = 1;
        if (max < 1) max = 1;
        if (max > 2000) max = 2000; // safety cap

        var cutoffUtc = DateTimeOffset.UtcNow.AddHours(-olderThanHours);
        var deleted = await store.DeleteRoomsOlderThanAsync(cutoffUtc, max, ct);

        return Ok(new
        {
            ok = true,
            olderThanHours,
            max,
            cutoffUtc,
            deleted
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/admin/rooms/close-idle/preview")]
    public async Task<IActionResult> PreviewCloseIdle(
        [FromQuery] int olderThanHours = 24,
        [FromQuery] int max = 200,
        CancellationToken ct = default)
    {
        olderThanHours = Math.Max(1, olderThanHours);
        max = Math.Clamp(max, 1, 2000);

        var rooms = await store.ListRoomsAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddHours(-olderThanHours);

        var candidates = new List<IdleRoomCandidate>();

        foreach (var r in rooms.OrderBy(x => x.CreatedAtUtc))
        {
            if (r.CreatedAtUtc > cutoff) continue;

            var state = await store.GetStateAsync(r.RoomId, ct);
            if (state is null) continue;

            if (state.ActiveGame != GameType.None) continue;

            var players = state.Players ?? [];
            var hostId = state.HostUserId;

            var hasNonHost = players.Any(p =>
                !string.IsNullOrWhiteSpace(p.UserId) &&
                !string.Equals(p.UserId, hostId, StringComparison.Ordinal));

            if (hasNonHost) continue;

            if (players.Count == 1 && !string.Equals(players[0].UserId, hostId, StringComparison.Ordinal))
                continue;

            candidates.Add(new IdleRoomCandidate(
                RoomId: r.RoomId,
                RoomCode: r.RoomCode,
                Name: r.Name,
                CreatedAtUtc: r.CreatedAtUtc,
                PlayerCount: players.Count,
                HostUserId: hostId,
                ActiveGame: state.ActiveGame.ToString(),
                Reason: "NoActiveGame + OnlyHost"
            ));

            if (candidates.Count >= max) break;
        }

        return Ok(new
        {
            ok = true,
            mode = "close-idle",
            olderThanHours,
            max,
            count = candidates.Count,
            rooms = candidates
        });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/admin/rooms/close-idle")]
    public async Task<IActionResult> CloseIdle(
        [FromQuery] int olderThanHours = 24,
        [FromQuery] int max = 200,
        CancellationToken ct = default)
    {
        var previewResult = await PreviewCloseIdle(olderThanHours, max, ct) as OkObjectResult;
        if (previewResult?.Value is null)
            return StatusCode(500, new { ok = false, error = "Preview failed" });

        var payload = (dynamic)previewResult.Value;
        var rooms = (IEnumerable<dynamic>)payload.rooms;

        var deleted = new List<object>();

        foreach (var x in rooms)
        {
            Guid roomId = x.RoomId;
            string roomCode = x.RoomCode;
            await store.DeleteRoomAsync(roomId, ct);
            await store.ReleaseRoomCodeAsync(roomCode, ct);

            deleted.Add(new { roomId, roomCode });
        }

        return Ok(new
        {
            ok = true,
            mode = "close-idle",
            deletedCount = deleted.Count,
            deleted
        });
    }
}