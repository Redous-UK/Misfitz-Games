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

        var roomId = Guid.NewGuid();

        // --- Choose room code (custom or generated) ---
        string code;

        if (!string.IsNullOrWhiteSpace(req.RoomCode))
        {
            code = NormalizeCustomCode(req.RoomCode);

            if (!IsValidCustomCode(code))
                return BadRequest(new { ok = false, error = "RoomCode must be 4-12 chars, A-Z and 0-9 only." });

            var reserved = await store.TryReserveRoomCodeAsync(code, roomId, ct);
            if (!reserved)
                return Conflict(new { ok = false, error = "RoomCode already in use." });
        }
        else
        {
            // Auto numeric 8-digit code; retry a few times until reservation succeeds
            code = "";
            for (var i = 0; i < 25; i++)
            {
                var candidate = NewNumericCode();
                if (await store.TryReserveRoomCodeAsync(candidate, roomId, ct))
                {
                    code = candidate;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(code))
                return StatusCode(503, new { ok = false, error = "Failed to allocate a room code. Try again." });
        }

        var room = new RoomDto(
            RoomId: roomId,
            Name: req.Name.Trim(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RoomCode: code
        );

        try
        {
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
        }
        catch
        {
            // If anything fails after reserving the code, release it so it isn't stuck.
            await store.ReleaseRoomCodeAsync(code, ct);
            throw;
        }

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
        return room is null
            ? NotFound(new { ok = false, error = "Room not found" })
            : Ok(room);
    }

    [HttpGet("/rooms/{roomRef}/state")]
    public async Task<IActionResult> GetState(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        return state is null
            ? NotFound(new { ok = false, error = "State not found" })
            : Ok(state);
    }

    private static string NormalizeCustomCode(string code)
        => (code ?? "").Trim().ToUpperInvariant();

    private static bool IsValidCustomCode(string code)
    {
        if (code.Length < 4 || code.Length > 12) return false;
        return code.All(ch => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'));
    }

    private static string NewNumericCode()
        => Random.Shared.Next(0, 100_000_000).ToString("D8");
}