using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class RoomsController(IRoomStateStore store) : ControllerBase
{
    [HttpPost("/rooms")]
    public async Task<IActionResult> Create([FromBody] RoomCreateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { ok = false, error = "Name is required" });

        var room = new RoomDto(
            RoomId: Guid.NewGuid(),
            Name: req.Name.Trim(),
            CreatedAtUtc: DateTimeOffset.UtcNow
        );

        await store.SaveRoomAsync(room, ct);

        // Initialize state
        var state = new RoomState(
            RoomId: room.RoomId,
            RoomName: room.Name,
            ActiveGame: GameType.None,
            GameState: null,
            UpdatedAtUtc: DateTimeOffset.UtcNow
        );

        await store.SaveStateAsync(state, ct);

        return Ok(room);
    }

    [HttpGet("/rooms")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await store.ListRoomsAsync(ct));

    [HttpGet("/rooms/{roomId:guid}")]
    public async Task<IActionResult> Get(Guid roomId, CancellationToken ct)
    {
        var room = await store.GetRoomAsync(roomId, ct);
        return room is null ? NotFound(new { ok = false, error = "Room not found" }) : Ok(room);
    }

    [HttpGet("/rooms/{roomId:guid}/state")]
    public async Task<IActionResult> GetState(Guid roomId, CancellationToken ct)
    {
        var state = await store.GetStateAsync(roomId, ct);
        return state is null ? NotFound(new { ok = false, error = "State not found" }) : Ok(state);
    }
}