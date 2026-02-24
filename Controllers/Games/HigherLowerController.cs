using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class HigherLowerController(
    IRoomStateStore store,
    RoomBroadcastService broadcaster
) : ControllerBase
{
    static int NextNum() => Random.Shared.Next(1, 100);

    static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue("userId")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "anon";

    async Task<(Guid roomId, RoomState state)?> LoadByCodeAsync(string roomCode, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomCode, ct);
        if (roomId is null) return null;

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return null;

        return (roomId.Value, state);
    }

    static HigherLowerState? GetGame(RoomState state)
        => state.GameState as HigherLowerState;

    // ----------------------------
    // Start
    // ----------------------------
    [Authorize(Policy = "MemberOrAdmin")]
    [HttpPost("/rooms/{roomCode}/games/higherlower/start")]
    public async Task<IActionResult> Start(string roomCode, CancellationToken ct)
    {
        var loaded = await LoadByCodeAsync(roomCode, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (roomId, state) = loaded.Value;

        var game = new HigherLowerState
        {
            Current = NextNum(),
            Next = NextNum(),
            Streak = 0,
            BestStreak = 0,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var nextState = state with
        {
            ActiveGame = GameType.HigherLower,
            GameState = game,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(nextState, ct);
        await store.IncrementGamesPlayedAsync(roomId, 1, ct);
        await broadcaster.BroadcastStateAsync(roomId, ct);

        return Ok(new { ok = true, state = nextState });
    }

    public sealed class GuessReq { public string? Pick { get; set; } }

    // ----------------------------
    // Guess (higher/lower)
    // ----------------------------
    [HttpPost("/rooms/{roomCode}/games/higherlower/guess")]
    public async Task<IActionResult> Guess(string roomCode, [FromBody] GuessReq req, CancellationToken ct)
    {
        var loaded = await LoadByCodeAsync(roomCode, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (roomId, state) = loaded.Value;

        if (state.ActiveGame != GameType.HigherLower)
            return BadRequest(new { ok = false, error = "Higher/Lower not active" });

        var game = GetGame(state);
        if (game is null)
            return StatusCode(500, new { ok = false, error = "Missing Higher/Lower state payload" });

        var pick = (req?.Pick ?? "").Trim().ToLowerInvariant();
        if (pick is not ("higher" or "lower"))
            return BadRequest(new { ok = false, error = "Pick must be 'higher' or 'lower'." });

        var userId = GetUserId(User);

        if (!game.AnswersByUserId.TryAdd(userId, pick))
            return BadRequest(new { ok = false, error = "Already answered this round." });

        game.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var nextState = state with
        {
            GameState = game,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(nextState, ct);
        await broadcaster.BroadcastStateAsync(roomId, ct);

        return Ok(new { ok = true, answers = game.AnswersByUserId.Count });
    }

    // ----------------------------
    // Reveal + advance
    // ----------------------------
    [Authorize(Policy = "MemberOrAdmin")]
    [HttpPost("/rooms/{roomCode}/games/higherlower/reveal")]
    public async Task<IActionResult> Reveal(string roomCode, CancellationToken ct)
    {
        var loaded = await LoadByCodeAsync(roomCode, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (roomId, state) = loaded.Value;

        if (state.ActiveGame != GameType.HigherLower)
            return BadRequest(new { ok = false, error = "Higher/Lower not active" });

        var game = GetGame(state);
        if (game is null)
            return StatusCode(500, new { ok = false, error = "Missing Higher/Lower state payload" });

        // Rule: ties count as "higher"
        var correct = (game.Next >= game.Current) ? "higher" : "lower";

        var anyCorrect = game.AnswersByUserId.Values.Any(v => v == correct);
        if (anyCorrect)
        {
            game.Streak += 1;
            game.BestStreak = Math.Max(game.BestStreak, game.Streak);
        }
        else
        {
            game.Streak = 0;
        }

        // advance
        game.Current = game.Next;
        game.Next = NextNum();
        game.AnswersByUserId.Clear();
        game.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var nextState = state with
        {
            GameState = game,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(nextState, ct);
        await store.IncrementGuessesTotalAsync(roomId, 1, ct);
        await broadcaster.BroadcastStateAsync(roomId, ct);

        return Ok(new { ok = true, correctPick = correct, state = nextState });
    }
}