using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.RiddleMeThis;
using Misfitz_Games.Services.Room;
using System.ComponentModel;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class RiddleMeThisController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    RiddleRepository riddles
) : RoomGameControllerBase(store, bus)
{
    private RiddleRepository Riddles { get; } = riddles;

    // Minimal riddle bank (swap to DB later)
    private static readonly (string category, string riddle, string answer)[] Bank =
    [
        ("", "I speak without a mouth and hear without ears. I have no body, but I come alive with wind. What am I?", "echo"),
        ("", "What has to be broken before you can use it?", "egg"),
        ("", "I’m tall when I’m young, and I’m short when I’m old. What am I?", "candle"),
        ("", "The more you take, the more you leave behind. What are they?", "footsteps"),
        ("", "What has keys but can’t open locks?", "piano"),
        ("", "What gets wetter the more it dries?", "towel"),
        ("", "I have branches, but no fruit, trunk or leaves. What am I?", "bank"),
        ("", "What can fill a room but takes up no space?", "light"),
        ("", "The more of this there is, the less you see. What is it?", "darkness"),
        ("", "What has one eye but can’t see?", "needle"),
        ("", "The more you take away from me, the bigger I become. What am I?", "hole"),
        ("", "What has many teeth but can’t bite?", "comb"),
        ("", "I’m found in socks, scarves and mittens; and often in the paws of playful kittens. What am I?", "yarn"),
        ("", "What can you catch but not throw?", "cold"),
        ("", "I have a neck but no head, and I wear a cap. What am I?", "bottle"),
        ("", "What can run but cant never walk?", "river"),
        ("", "What has a heart that doesn’t beat?", "artichoke"),
        ("", "I’m full of holes but I can still hold water. What am I?", "sponge"),
        ("", "What has a thumb and four fingers, but is not a hand?", "glove"),
        ("", "I canbe cracked, made, told, and played. What am I?", "joke")
    ];

    public sealed record StartReq(string? Category = null);
    public sealed record GuessReq(string Guess);

    [HttpPost("/rooms/{roomRef}/games/{game}/start")]
    public async Task<IActionResult> Start(string roomRef, [FromBody] StartReq? req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        // pick riddle
        var pick = await Riddles.GetRandomAsync(req?.Category, ct);
        if (pick is null)
            return BadRequest(new { error = "No riddles available (check DB / category / isActive)." });

        var st = new RiddleMeThisState(
            Round: 1,
            RiddleId: pick.Id,
            Category: pick.Category,
            Riddle: pick.Question,
            Answer: pick.Answer,
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

    [HttpPost("/rooms/{roomRef}/games/{game}/guess")]
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

    [HttpPost("/rooms/{roomRef}/games/{game}/reveal")]
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

    [HttpPost("/rooms/{roomRef}/games/{game}/next")]
    public async Task<IActionResult> Next(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        var pick = await Riddles.GetRandomAsync(st.Category, ct);
        if (pick is null)
            return BadRequest(new { error = "No riddles available (check DB / category / isActive)." });


        var nextSt = st with
        {
            Round = st.Round + 1,
            RiddleId = pick.Id,
            Category = pick.Category,
            Riddle = pick.Question,
            Answer = pick.Answer,
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