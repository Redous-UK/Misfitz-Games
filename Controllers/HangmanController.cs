using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;
using Misfitz_Games.Services.Games.Hangman;
using System.Text.Json;

namespace Misfitz_Games.Controllers;

[ApiController]
public sealed class HangmanController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster
) : ControllerBase
{
    private static bool TryGetHangman(RoomState room, out HangmanState hangman)
    {
        hangman = default!;

        if (room.GameState is HangmanState hs)
        {
            hangman = hs;
            return true;
        }

        if (room.GameState is JsonElement je)
        {
            try
            {
                var hs2 = je.Deserialize<HangmanState>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (hs2 is null) return false;
                hangman = hs2;
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    [HttpPost("/rooms/{roomIdOrCode}/games/hangman/start")]
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
        var publicState = HangmanView.PublicView(hangman);

        var updated = room with
        {
            ActiveGame = GameType.Hangman,
            GameState = hangman,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(updated, ct);
        await store.IncrementGamesPlayedAsync(roomId.Value, 1, ct);

        await broadcaster.BroadcastStateAsync(roomId.Value, new
        {
            roomId = roomId.Value,
            activeGame = (int)updated.ActiveGame,          // ✅ consistent (enum int)
            game = new { id = "hangman", state = publicState },
            utc = DateTimeOffset.UtcNow
        }, ct);

        await broadcaster.ToastAsync(roomId.Value, "Hangman started!", ct);

        // ✅ consistent: always return { state = ... }
        return Ok(new { state = publicState });
    }

    [HttpPost("/rooms/{roomIdOrCode}/games/hangman/guess")]
    public async Task<IActionResult> Guess(string roomIdOrCode, [FromBody] HangmanGuessRequest req, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        if (room.ActiveGame != GameType.Hangman || !TryGetHangman(room, out var hangman))
            return BadRequest(new { error = "Hangman is not active in this room." });

        var value = (req.Value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return BadRequest(new { error = "Guess value required." });

        var next = HangmanService.ApplyGuess(hangman, value, out var correct, out var message);
        var publicState = HangmanView.PublicView(next);

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
            activeGame = (int)updated.ActiveGame,
            game = new { id = "hangman", state = publicState },
            lastGuess = new { value, correct, message },
            utc = DateTimeOffset.UtcNow
        }, ct);

        if (!correct)
            await broadcaster.ToastAsync(roomId.Value, message, ct);

        return Ok(new
        {
            correct,
            message,
            state = publicState
        });
    }

    [HttpGet("/rooms/{roomIdOrCode}/games/hangman/state")]
    public async Task<IActionResult> State(string roomIdOrCode, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        if (room.ActiveGame != GameType.Hangman || !TryGetHangman(room, out var hangman))
            return Ok(new { state = new { game = "hangman", isActive = false } });

        return Ok(new { state = HangmanView.PublicView(hangman) });
    }
}
