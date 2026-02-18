using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.Contexto;
using Misfitz_Games.Services.Room;
using System.Security.Claims;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class ContextoController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    ContextoWordProvider words,
    ContextoEngine contexto
) : RoomGameControllerBase(store, bus)
{
    public sealed record ContextoStartRequest(string SecretWord);

    public sealed record GuessReq(string Guess);

    // ----------------------------
    // Contexto: Guess
    // ----------------------------
    // ✅ Anyone allowed to play (guest/member/admin) via cookie auth
    [Authorize(Policy = "Player")]
    [HttpPost("/rooms/{roomRef}/games/contexto/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] GuessReq req, CancellationToken ct)
    {
        var guess = (req?.Guess ?? "").Trim();
        if (guess.Length < 1 || guess.Length > 64)
            return BadRequest(new { ok = false, error = "Guess is required (1-64 chars)." });

        // ✅ cookie-auth identity
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Player";
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, error = "Not authenticated." });

        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        // Ensure Contexto is active AND load typed ContextoState safely
        if (!TryRequireGameState(room, GameType.Contexto, out ContextoState cs, out var err))
            return err!;

        // Apply guess in a typed way (avoid passing RoomState around)
        var (nextState, latestGuess) = ContextoApplyGuess(room, cs, userId, username, guess);

        await SaveRoomStateAsync(roomId, nextState, ct);
        await Store.IncrementGuessesTotalAsync(roomId, 1, ct);

        // Broadcast consistent envelope
        var publicState = ContextoPublic.From(cs: (ContextoState)nextState.GameState!);
        await BroadcastAsync(
            roomId,
            nextState.ActiveGame,
            gameId: "contexto",
            publicState: publicState,
            lastEvent: latestGuess is null
                ? new { type = "guess", guess, ignored = true }
                : new
                {
                    type = "guess",
                    guess = latestGuess.Guess,
                    percentage = latestGuess.Percentage,
                    rankOrScore = latestGuess.RankOrScore,
                    isWinner = latestGuess.IsWinner,
                    tsUtc = latestGuess.TsUtc,
                    userId = latestGuess.UserId,
                    username = latestGuess.Username
                },
            ct: ct
        );

        // Response: keep your current "ok" shape, but also include state for consistency
        return Ok(new
        {
            ok = true,
            state = publicState,
            latest = latestGuess is null
                ? null
                : new
                {
                    latestGuess.Guess,
                    latestGuess.Percentage,
                    latestGuess.RankOrScore,
                    latestGuess.IsWinner,
                    latestGuess.TsUtc
                }
        });
    }

    // ----------------------------
    // Contexto: Start (explicit secret)
    // ----------------------------
    // Keep your existing GUID route (admin panel uses it today)
    [HttpPost("/rooms/{roomId:guid}/games/contexto/start")]
    public async Task<IActionResult> StartContexto(Guid roomId, [FromBody] ContextoStartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SecretWord))
            return BadRequest(new { ok = false, error = "SecretWord is required" });

        var room = await Store.GetStateAsync(roomId, ct);
        if (room is null) return NotFound(new { ok = false, error = "Room state not found" });

        var cs = ContextoEngine.NewRound(req.SecretWord);
        var next = room with
        {
            ActiveGame = GameType.Contexto,
            GameState = cs,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, next, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        var publicState = ContextoPublic.From(cs);
        await BroadcastAsync(roomId, next.ActiveGame, "contexto", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId, "Contexto started!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Contexto: Next round (random secret)
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/contexto/next")]
    public async Task<IActionResult> NextContextoRound(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (roomId, room) = loaded.Value;
        var secret = words.NextSecret();

        var cs = ContextoEngine.NewRound(secret);
        var next = room with
        {
            ActiveGame = GameType.Contexto,
            GameState = cs,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, next, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        var publicState = ContextoPublic.From(cs);
        await BroadcastAsync(roomId, next.ActiveGame, "contexto", publicState, new { type = "next" }, ct);
        await ToastAsync(roomId, "New Contexto round!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Contexto: Public state endpoint (matches Hangman pattern)
    // ----------------------------
    [HttpGet("/rooms/{roomRef}/games/contexto/state")]
    public async Task<IActionResult> State(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (_, room) = loaded.Value;

        if (room.ActiveGame != GameType.Contexto ||
            !GameStateJson.TryDeserialize(room.GameState, out ContextoState cs))
        {
            return Ok(new { ok = true, state = new { game = "contexto", isActive = false } });
        }

        return Ok(new { ok = true, state = ContextoPublic.From(cs) });
    }

    // ----------------------------
    // Leaderboard (keep route; make it robust against JsonElement)
    // ----------------------------
    [HttpGet("/rooms/{roomRef}/leaderboard")]
    public async Task<IActionResult> Leaderboard(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return NotFound(new { ok = false, error = "Room not found" });

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame == GameType.Contexto &&
            GameStateJson.TryDeserialize(room.GameState, out ContextoState cs))
        {
            var top = cs.ScoresByUserId
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new { userId = kv.Key, score = kv.Value })
                .ToList();

            return Ok(new { ok = true, roomId, game = "contexto", top });
        }

        return Ok(new { ok = true, roomId, game = room.ActiveGame.ToString(), top = Array.Empty<object>() });
    }

    // ----------------------------
    // Internal: apply guess in a typed, consistent way
    // ----------------------------
    private (RoomState nextRoom, ContextoGuess? latest) ContextoApplyGuess(
        RoomState room,
        ContextoState _,
        string userId,
        string username,
        string guess)
    {
        // If you prefer to keep ContextoEngine.ApplyGuess(RoomState, ...)
        // you can still call it here, then re-materialize ContextoState.
        // But a typed path is cleaner.

        // 1) Build the updated ContextoState using your engine
        //    (Assumption: your engine can accept ContextoState directly or can be adapted)
        //    If your current engine ONLY accepts RoomState, replace this block with:
        //      var updatedRoom = contexto.ApplyGuess(room, userId, username, guess);
        //      var cs2 = (ContextoState)updatedRoom.GameState!;
        //      return (updatedRoom, cs2.RecentGuesses?.FirstOrDefault());

        var updatedRoom = contexto.ApplyGuess(room, userId, username, guess);

        // Ensure GameState is typed (in case ApplyGuess kept it as JsonElement)
        if (!GameStateJson.TryDeserialize(updatedRoom.GameState, out ContextoState cs2))
        {
            // fallback: treat as ignored
            return (updatedRoom, null);
        }

        // keep the room state carrying the typed ContextoState (important for persistence)
        updatedRoom = updatedRoom with { GameState = cs2, UpdatedAtUtc = DateTimeOffset.UtcNow };

        var latest = cs2.RecentGuesses?.FirstOrDefault();
        return (updatedRoom, latest);
    }
}

// ----------------------------
// Public projection for Contexto (so clients never depend on internal model casing)
// ----------------------------
internal static class ContextoPublic
{
    public static object From(ContextoState cs)
        => new
        {
            game = "contexto",
            isActive = cs.IsActive,
            startedAtUtc = cs.StartedAtUtc,
            endedAtUtc = cs.EndedAtUtc,
            recentGuesses = cs.RecentGuesses
                .Take(10)
                .Select(g => new
                {
                    g.UserId,
                    g.Username,
                    g.Guess,
                    g.Percentage,
                    g.RankOrScore,
                    g.IsWinner,
                    g.TsUtc
                })
                .ToList(),
            top = cs.ScoresByUserId
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new { userId = kv.Key, score = kv.Value })
                .ToList()
        };
}
