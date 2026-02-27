using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.RiddleMeThis;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class RiddleMeThisController(
    Misfitz_Games.Services.Room.IRoomStateStore store,
    Misfitz_Games.Services.Room.RoomGameBroadcaster bus
) : RoomGameControllerBase(store, bus)
{
    // Minimal riddle bank (swap to DB later)
    private static readonly (string riddle, string answer)[] Bank =
    [
        ("I speak without a mouth and hear without ears. I have no body, but I come alive with wind. What am I?", "echo"),
        ("The more you take, the more you leave behind. What are they?", "footsteps"),
        ("What has keys but can’t open locks?", "piano"),
        ("What gets wetter the more it dries?", "towel"),
    ];

    public sealed record StartReq(int? Seed = null);
    public sealed record GuessReq(string Guess);

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/start")]
    public async Task<IActionResult> Start(string roomRef, [FromBody] StartReq? req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        // pick riddle
        var seed = req?.Seed ?? Environment.TickCount;
        var idx = Math.Abs(seed) % Bank.Length;
        var (r, a) = Bank[idx];

        var st = new RiddleMeThisState(
            Round: 1,
            Riddle: r,
            Answer: a,
            IsSolved: false,
            SolvedByUserId: null,
            StartedAtUtc: DateTimeOffset.UtcNow,
            SolvedAtUtc: null,
            RecentGuesses: []
        );

        var next = room with
        {
            ActiveGame = GameType.RiddleMeThis,
            GameState = st,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, next, ct);

        // broadcast public state
        var pub = RiddleMeThisView.PublicView(st);
        await BroadcastAsync(roomId, GameType.RiddleMeThis, "riddle_me_this", pub, lastEvent: new { type = "started" }, ct);

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] GuessReq req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Guess))
            return BadRequest(new { error = "Guess is required." });

        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        if (st.IsSolved)
            return Ok(new { ok = true, alreadySolved = true });

        // TODO: Replace with your actual auth/user id resolver
        var userId = User?.Identity?.Name ?? "guest";

        static string Norm(string s) => new([.. s.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit)]);
        var isCorrect = Norm(req.Guess) == Norm(st.Answer);

        var guesses = st.RecentGuesses.ToList();
        guesses.Add(new RiddleGuess(userId, req.Guess.Trim(), isCorrect, DateTimeOffset.UtcNow));
        if (guesses.Count > 50) guesses.RemoveRange(0, guesses.Count - 50);

        var nextSt = st with
        {
            RecentGuesses = guesses,
            IsSolved = isCorrect || st.IsSolved,
            SolvedByUserId = isCorrect ? userId : st.SolvedByUserId,
            SolvedAtUtc = isCorrect ? DateTimeOffset.UtcNow : st.SolvedAtUtc
        };

        var nextRoom = room with
        {
            GameState = nextSt,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, nextRoom, ct);

        var pub = RiddleMeThisView.PublicView(nextSt);
        await BroadcastAsync(
            roomId,
            GameType.RiddleMeThis,
            "riddle_me_this",
            pub,
            lastEvent: new { type = "guess", userId, isCorrect },
            ct
        );

        return Ok(new { ok = true, isCorrect });
    }

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/reveal")]
    public async Task<IActionResult> Reveal(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        // reveal via toast + event, without exposing answer in public state
        await ToastAsync(roomId, $"Answer: {st.Answer}", ct);
        await BroadcastAsync(roomId, GameType.RiddleMeThis, "riddle_me_this",
            RiddleMeThisView.PublicView(st),
            lastEvent: new { type = "reveal" },
            ct);

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/next")]
    public async Task<IActionResult> Next(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        var idx = Random.Shared.Next(0, Bank.Length);
        var (r, a) = Bank[idx];

        var nextSt = st with
        {
            Round = st.Round + 1,
            Riddle = r,
            Answer = a,
            IsSolved = false,
            SolvedByUserId = null,
            StartedAtUtc = DateTimeOffset.UtcNow,
            SolvedAtUtc = null,
            RecentGuesses = []
        };

        var nextRoom = room with
        {
            GameState = nextSt,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, nextRoom, ct);

        var pub = RiddleMeThisView.PublicView(nextSt);
        await BroadcastAsync(roomId, GameType.RiddleMeThis, "riddle_me_this", pub, lastEvent: new { type = "next" }, ct);

        return Ok(new { ok = true });
    }
}