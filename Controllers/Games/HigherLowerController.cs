using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.HigherLower;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class HigherLowerController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    HigherLowerService higherLower
) : RoomGameControllerBase(store, bus)
{
    public sealed class HigherLowerGuessRequest
    {
        public string? Choice { get; set; } // "higher" | "lower"
    }

    // ----------------------------
    // Higher / Lower: Start
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/higher_lower/start")]
    public async Task<IActionResult> Start(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var round = higherLower.NewGame();
        var updated = room with
        {
            ActiveGame = GameType.HigherLower,
            GameState = round,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        var publicState = HigherLowerView.PublicView(round);
        await BroadcastAsync(roomId, updated.ActiveGame, "higher_lower", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId, "Higher / Lower started!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Higher / Lower: Guess
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/higher_lower/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] HigherLowerGuessRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState(room, GameType.HigherLower, out HigherLowerState round, out var err))
            return err!;

        var choice = (req?.Choice ?? "").Trim();
        if (string.IsNullOrWhiteSpace(choice))
            return BadRequest(new { ok = false, error = "Choice is required (higher|lower)." });

        string? message;
        try
        {
            higherLower.GuessInPlace(round, choice);

            // Friendly message for UI + toast
            message = round.LastWasCorrect == true
                ? "Correct!"
                : "Unlucky!";
        }
        catch (Exception ex)
        {
            return BadRequest(new { ok = false, error = ex.Message });
        }

        var updated = room with
        {
            GameState = round,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGuessesTotalAsync(roomId, 1, ct);

        var publicState = HigherLowerView.PublicView(round);
        var lastEvent = new
        {
            type = "guess",
            choice = round.LastChoice,
            correct = round.LastWasCorrect,
            revealed = round.RevealedNext?.Label,
            streak = round.Streak,
            bestStreak = round.BestStreak,
            message
        };

        await BroadcastAsync(roomId, updated.ActiveGame, "higher_lower", publicState, lastEvent, ct);

        if (round.LastWasCorrect == false)
            await ToastAsync(roomId, $"{message} Streak ended at {round.Streak}.", ct);

        return Ok(new { ok = true, state = publicState, lastEvent });
    }

    // ----------------------------
    // Higher / Lower: Continue (after loss reveal)
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/higher_lower/continue")]
    public async Task<IActionResult> Continue(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState(room, GameType.HigherLower, out HigherLowerState round, out var err))
            return err!;

        if (round.Status != HigherLowerStatus.Revealed)
            return BadRequest(new { ok = false, error = "Not in revealed state." });

        higherLower.ContinueAfterLossInPlace(round);

        var updated = room with
        {
            GameState = round,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = HigherLowerView.PublicView(round);
        await BroadcastAsync(roomId, updated.ActiveGame, "higher_lower", publicState, new { type = "continue" }, ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Higher / Lower: Stop
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/higher_lower/stop")]
    public async Task<IActionResult> Stop(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var updated = room with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        await BroadcastAsync(roomId, updated.ActiveGame, "higher_lower", new { game = "higher_lower", isActive = false }, new { type = "stop" }, ct);
        await ToastAsync(roomId, "Higher / Lower stopped.", ct);

        return Ok(new { ok = true });
    }

    // ----------------------------
    // Higher / Lower: State
    // ----------------------------
    [HttpGet("/rooms/{roomRef}/games/higher_lower/state")]
    public async Task<IActionResult> State(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (_, room) = loaded.Value;

        if (room.ActiveGame != GameType.HigherLower ||
            !GameStateJson.TryDeserialize(room.GameState, out HigherLowerState round))
        {
            return Ok(new { ok = true, state = new { game = "higher_lower", isActive = false } });
        }

        return Ok(new { ok = true, state = HigherLowerView.PublicView(round) });
    }
}