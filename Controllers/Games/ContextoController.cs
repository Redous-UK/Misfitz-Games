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
    public sealed record ContextoStartRequest(string? SecretWord);

    public sealed record GuessReq(string? Guess);

    private static bool IsContextoRoute(string? game)
        => string.IsNullOrWhiteSpace(game)
           || string.Equals(game, "contexto", StringComparison.OrdinalIgnoreCase);

    // ----------------------------
    // Contexto: Guess
    // ----------------------------
    [Authorize(Policy = "Player")]
    [HttpPost("/rooms/{roomRef}/games/contexto/guess")]
    public async Task<IActionResult> Guess(string roomRef, string? game, [FromBody] GuessReq? req, CancellationToken ct)
    {
        if (!IsContextoRoute(game))
            return NotFound(new { ok = false, error = "Game not found." });

        var guess = (req?.Guess ?? "").Trim();
        if (guess.Length < 1 || guess.Length > 64)
            return BadRequest(new { ok = false, error = "Guess is required (1-64 chars)." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Player";
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { ok = false, error = "Not authenticated." });

        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState(room, GameType.Contexto, out ContextoState cs, out var err))
            return err!;

        var (nextState, latestGuess) = ContextoApplyGuess(room, cs, userId, username, guess);

        await SaveRoomStateAsync(roomId, nextState, ct);
        await Store.IncrementGuessesTotalAsync(roomId, 1, ct);

        var typedState = nextState.GameState as ContextoState;
        if (typedState is null && !GameStateJson.TryDeserialize(nextState.GameState, out typedState))
            return StatusCode(500, new { ok = false, error = "Failed to read updated Contexto state." });

        var publicState = ContextoPublic.From(typedState!);

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
    [HttpPost("/rooms/{roomRef}/games/contexto/start")]
    public async Task<IActionResult> StartContexto(string roomRef, string? game, [FromBody] ContextoStartRequest? req, CancellationToken ct)
    {
        if (!IsContextoRoute(game))
            return NotFound(new { ok = false, error = "Game not found." });

        if (string.IsNullOrWhiteSpace(req?.SecretWord))
            return BadRequest(new { ok = false, error = "SecretWord is required" });

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
                Players: [],
                HostUserId: null
            );
        }

        var cs = ContextoEngine.NewRound(req.SecretWord.Trim());
        var next = room with
        {
            ActiveGame = GameType.Contexto,
            GameState = cs,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId.Value, next, ct);
        await Store.IncrementGamesPlayedAsync(roomId.Value, 1, ct);

        var publicState = ContextoPublic.From(cs);
        await BroadcastAsync(roomId.Value, next.ActiveGame, "contexto", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId.Value, "Contexto started!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Contexto: Next round (random secret)
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/contexto/next")]
    public async Task<IActionResult> NextContextoRound(string roomRef, string? game, CancellationToken ct)
    {
        if (!IsContextoRoute(game))
            return NotFound(new { ok = false, error = "Game not found." });

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
                Players: [],
                HostUserId: null
            );
        }

        var secret = words.NextSecret();
        var cs = ContextoEngine.NewRound(secret);
        var next = room with
        {
            ActiveGame = GameType.Contexto,
            GameState = cs,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId.Value, next, ct);
        await Store.IncrementGamesPlayedAsync(roomId.Value, 1, ct);

        var publicState = ContextoPublic.From(cs);
        await BroadcastAsync(roomId.Value, next.ActiveGame, "contexto", publicState, new { type = "next" }, ct);
        await ToastAsync(roomId.Value, "New Contexto round!", ct);

        return Ok(new { ok = true, state = publicState });
    }

    // ----------------------------
    // Contexto: Public state endpoint
    // ----------------------------
    [HttpGet("/rooms/{roomRef}/games/contexto/state")]
    public async Task<IActionResult> State(string roomRef, string? game, CancellationToken ct)
    {
        if (!IsContextoRoute(game))
            return NotFound(new { ok = false, error = "Game not found." });

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
    // Internal: apply guess in a typed, consistent way
    // ----------------------------
    private (RoomState nextRoom, ContextoGuess? latest) ContextoApplyGuess(
        RoomState room,
        ContextoState _,
        string userId,
        string username,
        string guess)
    {
        var updatedRoom = contexto.ApplyGuess(room, userId, username, guess);

        if (!GameStateJson.TryDeserialize(updatedRoom.GameState, out ContextoState cs2))
            return (updatedRoom, null);

        updatedRoom = updatedRoom with { GameState = cs2, UpdatedAtUtc = DateTimeOffset.UtcNow };

        var latest = cs2.RecentGuesses?.FirstOrDefault();
        return (updatedRoom, latest);
    }
}

internal static class ContextoPublic
{
    public static object From(ContextoState cs)
        => new
        {
            game = "contexto",
            isActive = cs.IsActive,
            startedAtUtc = cs.StartedAtUtc,
            endedAtUtc = cs.EndedAtUtc,
            recentGuesses = (cs.RecentGuesses ?? Enumerable.Empty<ContextoGuess>())
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
            top = (cs.ScoresByUserId ?? [])
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new { userId = kv.Key, score = kv.Value })
                .ToList()
        };
}