using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Services.Games.Hangman;

namespace Misfitz_Games.Controllers;

[ApiController]
public sealed class HangmanController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster
) : ControllerBase
{
    [HttpPost("/rooms/{roomIdOrCode}/hangman/start")]
    public async Task<IActionResult> Start(string roomIdOrCode, [FromBody] HangmanStartRequest req, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        var word = (req.Word ?? "").Trim();
        if (string.IsNullOrWhiteSpace(word))
            return BadRequest(new { error = "Word required (for now)." });

        var maxWrong = (req.MaxWrong is > 0) ? req.MaxWrong.Value : 6;

        var hangman = HangmanService.StartNew(word, maxWrong);

        var updated = room with
        {
            ActiveGame = GameType.Hangman,
            GameState = hangman,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(updated, ct);
        await store.IncrementGamesPlayedAsync(roomId.Value, 1, ct);

        // Broadcast full room state (recommended) OR just game public state
        await broadcaster.BroadcastStateAsync(roomId.Value, new
        {
            roomId = roomId.Value,
            activeGame = "hangman",
            game = HangmanView.PublicView(hangman),
            utc = DateTimeOffset.UtcNow
        }, ct);

        await broadcaster.ToastAsync(roomId.Value, "Hangman started!", ct);

        return Ok(HangmanView.PublicView(hangman));
    }

    [HttpPost("/rooms/{roomIdOrCode}/hangman/guess")]
    public async Task<IActionResult> Guess(string roomIdOrCode, [FromBody] HangmanGuessRequest req, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        if (room.ActiveGame != GameType.Hangman || room.GameState is not HangmanState hangman)
            return BadRequest(new { error = "Hangman is not active in this room." });

        var value = (req.Value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return BadRequest(new { error = "Guess value required." });

        var next = HangmanService.ApplyGuess(hangman, value, out var correct, out var message);

        var updated = room with
        {
            GameState = next,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(updated, ct);
        await store.IncrementGuessesTotalAsync(roomId.Value, 1, ct);

        await broadcaster.BroadcastStateAsync(roomId.Value, new
        {
            roomId = roomId.Value,
            activeGame = "hangman",
            guess = new { value, correct, message },
            game = HangmanView.PublicView(next),
            utc = DateTimeOffset.UtcNow
        }, ct);

        if (!correct)
            await broadcaster.ToastAsync(roomId.Value, message, ct);

        return Ok(new { correct, message, state = HangmanView.PublicView(next) });
    }

    [HttpGet("/rooms/{roomIdOrCode}/hangman/state")]
    public async Task<IActionResult> State(string roomIdOrCode, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        if (room.ActiveGame != GameType.Hangman || room.GameState is not HangmanState hangman)
            return Ok(new { game = "hangman", isActive = false });

        return Ok(HangmanView.PublicView(hangman));
    }
}
