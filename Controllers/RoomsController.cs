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
            RoomCode: RoomCodeGenerator.NewCode(),
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

    [HttpGet("/rooms/{roomRef}")]
    public async Task<IActionResult> Get(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var room = await store.GetRoomAsync(roomId.Value, ct);
        return room is null ? NotFound(new { ok = false, error = "Room not found" }) : Ok(room);
    }

    [HttpGet("/rooms/{roomRef}/state")]
    public async Task<IActionResult> GetState(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        return state is null ? NotFound(new { ok = false, error = "State not found" }) : Ok(state);
    }
}