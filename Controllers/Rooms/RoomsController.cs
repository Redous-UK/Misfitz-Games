using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Rooms;

[ApiController]
public class RoomsController(IRoomStateStore store, RoomBroadcastService broadcaster, AppDbContext db) : ControllerBase
{

    private readonly AppDbContext _db = db;

    private static string NormalizeCustomCode(string code)
        => (code ?? "").Trim().ToUpperInvariant();

    private static bool IsValidCustomCode(string code)
    {
        if (code.Length < 4 || code.Length > 12) return false;
        return code.All(ch => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'));
    }

    private static string NewNumericCode()
        => Random.Shared.Next(0, 100_000_000).ToString("D8");

    private static bool TryParseGameType(string game, out GameType gameType)
    {
        switch ((game ?? "").Trim().ToLowerInvariant())
        {
            case "contexto":
                gameType = GameType.Contexto;
                return true;
            case "deal":
                gameType = GameType.Deal;
                return true;
            case "hangman":
                gameType = GameType.Hangman;
                return true;
            case "trivia":
                gameType = GameType.Trivia;
                return true;
            case "higherlower":
            case "higher-lower":
                gameType = GameType.HigherLower;
                return true;
            case "riddle":
            case "riddlemethis":
                gameType = GameType.RiddleMeThis;
                return true;
            default:
                gameType = GameType.None;
                return false;
        }
    }

    [HttpGet("/rooms")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rooms = await store.ListRoomsAsync(ct);
        var items = new List<RoomSummaryDto>(rooms.Count);

        foreach (var r in rooms)
        {
            var state = await store.GetStateAsync(r.RoomId, ct);

            var activeGame = state?.ActiveGame ?? GameType.None;
            var players = state?.Players?.Count ?? 0;

            items.Add(new RoomSummaryDto(
                RoomId: r.RoomId,
                Name: r.Name,
                RoomCode: r.RoomCode,
                CreatedAtUtc: r.CreatedAtUtc,
                PlayerCount: players,
                HasActiveGame: activeGame != GameType.None,
                ActiveGame: activeGame == GameType.None ? null : activeGame.ToString()
            ));
        }

        return Ok(new { rooms = items });
    }

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
    public async Task<IActionResult> State(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var room = await store.GetStateAsync(roomId.Value, ct);
        if (room is null) return NotFound(new { error = "Room state not found." });

        var pub = RoomStateProjector.ToPublic(room);
        return Ok(new { state = pub });
    }

    [HttpGet("/room/resolve/{roomRef}")]
    public async Task<IActionResult> Resolve(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { error = "Room not found." });

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return NotFound(new { error = "Room state not found." });

        return Ok(new { roomId = roomId.Value, state });
    }

    [HttpPost("/rooms/{roomRef}/games/stop")]
    public async Task<IActionResult> Stop(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var state = await store.GetStateAsync(roomId.Value, ct);
        if (state is null) return NotFound(new { ok = false, error = "Room state not found" });

        var next = state with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await store.SaveStateAsync(next, ct);

        var pub = RoomStateProjector.ToPublic(next);
        await broadcaster.BroadcastStateAsync(roomId.Value, pub, ct);

        return Ok(new { ok = true, state = pub });
    }

    [HttpGet("/rooms/{roomRef}/leaderboard")]
    public async Task<IActionResult> GetRoomLeaderboard(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        var leaderboard = await store.GetLeaderboardAsync(roomId.Value, 20, ct);

        return Ok(new
        {
            roomId = roomId.Value,
            leaderboard
        });
    }

    [HttpGet("/rooms/{roomRef}/leaderboard/{game}")]
    public async Task<IActionResult> GetGameLeaderboard(string roomRef, string game, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        if (!TryParseGameType(game, out var gameType))
            return BadRequest(new { ok = false, error = "Unknown game type" });

        var leaderboard = await store.GetLeaderboardAsync(roomId.Value, gameType, 20, ct);

        return Ok(new
        {
            roomId = roomId.Value,
            game = gameType.ToString(),
            leaderboard
        });
    }

    [HttpGet("/rooms/{roomRef}/player/{userId}/stats")]
    public async Task<IActionResult> GetPlayerStats(string roomRef, string userId, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null)
            return NotFound(new { ok = false, error = "Room not found" });

        var stats = await store.GetLeaderboardPlayerAsync(roomId.Value, userId, ct);
        if (stats is null)
            return NotFound(new { ok = false, error = "Player stats not found for this room" });

        return Ok(new
        {
            roomId = roomId.Value,
            stats
        });
    }

/*    [HttpGet("/rooms/{roomRef}/leaderboard")]
    public async Task<IActionResult> GetRoomLeaderboard(string roomRef, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var scores = await db.RoomPlayerScores
            .Where(x => x.RoomId == roomId.Value)
            .OrderByDescending(x => x.TotalScore)
            .Take(20)
            .Select(x => new
            {
                x.UserId,
                x.Username,
                x.TotalScore,
                x.TriviaScore,
                x.HangmanScore,
                x.ContextoScore,
                x.RiddleScore,
                x.HigherLowerScore,
                x.DealScore,
                x.Wins,
                x.GamesPlayed,
                x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new { roomId = roomId.Value, leaderboard = scores });
    }

    [HttpGet("/rooms/{roomRef}/leaderboard/{game}")]
    public async Task<IActionResult> GetGameLeaderboard(string roomRef, string game, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var baseQuery = db.RoomPlayerScores.Where(x => x.RoomId == roomId.Value);

        var results = game.Trim().ToLowerInvariant() switch
        {
            "trivia" => await baseQuery.OrderByDescending(x => x.TriviaScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.TriviaScore, x.TotalScore, Game = "Trivia", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            "hangman" => await baseQuery.OrderByDescending(x => x.HangmanScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.HangmanScore, x.TotalScore, Game = "Hangman", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            "contexto" => await baseQuery.OrderByDescending(x => x.ContextoScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.ContextoScore, x.TotalScore, Game = "Contexto", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            "riddle" or "riddlemethis" => await baseQuery.OrderByDescending(x => x.RiddleScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.RiddleScore, x.TotalScore, Game = "RiddleMeThis", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            "higherlower" or "higher-lower" => await baseQuery.OrderByDescending(x => x.HigherLowerScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.HigherLowerScore, x.TotalScore, Game = "HigherLower", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            "deal" => await baseQuery.OrderByDescending(x => x.DealScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.DealScore, x.TotalScore, Game = "Deal", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct),

            _ => await baseQuery.OrderByDescending(x => x.TotalScore).ThenBy(x => x.Username)
                .Take(20)
                .Select(x => new { x.UserId, x.Username, Score = x.TotalScore, x.TotalScore, Game = "Total", x.Wins, x.GamesPlayed, x.UpdatedAtUtc })
                .ToListAsync(ct)
        };

        return Ok(new
        {
            roomId = roomId.Value,
            game,
            leaderboard = results
        });
    }

    [HttpGet("/rooms/{roomRef}/player/{userId}/stats")]
    public async Task<IActionResult> GetPlayerStats(string roomRef, string userId, CancellationToken ct)
    {
        var roomId = await store.ResolveRoomIdAsync(roomRef, ct);
        if (roomId is null) return NotFound(new { ok = false, error = "Room not found" });

        var stats = await db.RoomPlayerScores
            .Where(x => x.RoomId == roomId.Value && x.UserId == userId)
            .Select(x => new
            {
                x.UserId,
                x.Username,
                x.TotalScore,
                x.TriviaScore,
                x.HangmanScore,
                x.ContextoScore,
                x.RiddleScore,
                x.HigherLowerScore,
                x.DealScore,
                x.Wins,
                x.GamesPlayed,
                x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (stats is null)
            return NotFound(new { ok = false, error = "Player stats not found for this room" });

        return Ok(new { roomId = roomId.Value, stats });
    }*/
} 
