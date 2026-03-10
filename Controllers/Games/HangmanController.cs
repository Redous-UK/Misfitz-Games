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
    [HttpPost("/rooms/{roomRef}/games/{game}/start")]
    [HttpPost("/rooms/{roomRef}/games/hangman/start")]
    public async Task<IActionResult> Start(string roomRef, [FromBody] HangmanStartRequest req, CancellationToken ct)
    {
        var roomId = await Store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        var room = await Store.GetStateAsync(roomId.Value, ct);
        if (room is null)
        {
            var meta = await Store.GetRoomAsync(roomId.Value, ct);
            if (meta is null)
                return NotFound(new { ok = false, error = "Room not found" });

            room = new RoomState(
                RoomId: meta.RoomId,
                RoomName: meta.Name,
                ActiveGame: GameType.None,
                GameState: null,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Players: new List<PlayerPresence>(),
                HostUserId: null
            );
        }

        var word = (req.Word ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(word))
            return BadRequest(new { ok = false, error = "Word required (for now)." });

        var maxWrong = (req.MaxWrong is > 0) ? req.MaxWrong.Value : 6;

        var hangman = HangmanService.StartNew(word, maxWrong);
        var publicState = HangmanView.PublicView(hangman);

        var updated = room with
        {
            ActiveGame = GameType.Hangman,
            GameState = hangman,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId.Value, updated, ct);
        await Store.IncrementGamesPlayedAsync(roomId.Value, 1, ct);

        await BroadcastAsync(roomId.Value, updated.ActiveGame, "hangman", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId.Value, "Hangman started!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    [HttpPost("/rooms/{roomRef}/games/{game}/guess")]
    [HttpPost("/rooms/{roomRef}/games/hangman/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] HangmanGuessRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState(room, GameType.Hangman, out HangmanState hangman, out var err))
            return err!;

        var value = (req.Value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return BadRequest(new { ok = false, error = "Guess value required." });

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

        return Ok(new { ok = true, correct, message, state = publicState });
    }

    [HttpGet("/rooms/{roomRef}/games/{game}/state")]
    [HttpGet("/rooms/{roomRef}/games/hangman/state")]
    public async Task<IActionResult> State(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (_, room) = loaded.Value;

        if (room.ActiveGame != GameType.Hangman ||
            !GameStateJson.TryDeserialize(room.GameState, out HangmanState hangman))
        {
            return Ok(new { ok = true, state = new { game = "hangman", isActive = false } });
        }

        return Ok(new { ok = true, state = HangmanView.PublicView(hangman) });
    }
}