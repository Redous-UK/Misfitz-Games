using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Room;
using System.Security.Claims;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class HangmanController(
    IRoomStateStore store,
    RoomGameBroadcaster bus
) : RoomGameControllerBase(store, bus)
{
    [HttpPost("/rooms/{roomIdOrCode}/games/hangman/start")]
    public async Task<IActionResult> Start(string roomIdOrCode, [FromBody] HangmanStartRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomIdOrCode, ct);
        if (loaded is null) return RoomNotFound(); // if you want to distinguish, split LoadRoomStateAsync

        var (roomId, room) = loaded.Value;

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

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        await BroadcastAsync(roomId, updated.ActiveGame, "hangman", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId, "Hangman started!", ct);

        return Ok(new { state = publicState });
    }

    [HttpPost("/rooms/{roomIdOrCode}/games/hangman/guess")]
    public async Task<IActionResult> Guess(string roomIdOrCode, [FromBody] HangmanGuessRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomIdOrCode, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState(room, GameType.Hangman, out HangmanState hangman, out var err))
            return err!;

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

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGuessesTotalAsync(roomId, 1, ct);

        var lastEvent = new { type = "guess", value, correct, message };
        await BroadcastAsync(roomId, updated.ActiveGame, "hangman", publicState, lastEvent, ct);

        if (!correct)
            await ToastAsync(roomId, message, ct);

        return Ok(new { correct, message, state = publicState });
    }

    [HttpGet("/rooms/{roomIdOrCode}/games/hangman/state")]
    public async Task<IActionResult> State(string roomIdOrCode, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomIdOrCode, ct);
        if (loaded is null) return RoomNotFound();

        var (_, room) = loaded.Value;

        if (room.ActiveGame != GameType.Hangman ||
            !GameStateJson.TryDeserialize(room.GameState, out HangmanState hangman))
        {
            return Ok(new { state = new { game = "hangman", isActive = false } });
        }

        return Ok(new { state = HangmanView.PublicView(hangman) });
    }
}
