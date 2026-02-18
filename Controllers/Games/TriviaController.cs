using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.Trivia;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Games;

public sealed class TriviaController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    TriviaService trivia
) : RoomGameControllerBase(store, bus)
{
    // ----------------------------
    // Requests
    // ----------------------------
    public sealed class TriviaStartRequest
    {
        public string? Difficulty { get; set; } // easy|medium|hard
    }

    public sealed class TriviaAnswerRequest
    {
        public string? UserId { get; set; }
        public string? Choice { get; set; } // A/B/C/D
    }

    // ----------------------------
    // Start
    // POST /rooms/{roomRef}/games/trivia/start
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/trivia/start")]
    public async Task<IActionResult> Start(string roomRef, [FromBody] TriviaStartRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var difficulty = (req.Difficulty ?? "easy").Trim().ToLowerInvariant();
        if (difficulty is not ("easy" or "medium" or "hard"))
            return BadRequest(new { error = "Difficulty must be easy, medium, or hard." });

        var q = await trivia.GetOneAsync(difficulty, ct);
        if (q is null)
            return BadRequest(new { error = "No trivia question available right now." });

        // Carry scores forward if we already had trivia in progress.
        var prev = room.GameState as TriviaRoundState;

        var round = new TriviaRoundState(
            Active: true,
            Current: q,
            AskedAtUtc: DateTimeOffset.UtcNow,
            Revealed: false,
            ScoresByUserId: prev?.ScoresByUserId ?? [],
            AnsweredThisQuestionUserIds: []
        );

        var updated = room with
        {
            ActiveGame = GameType.Trivia,
            GameState = round,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        var publicState = TriviaPublic.From((TriviaRoundState)updated.GameState!);

        await BroadcastAsync(roomId, updated.ActiveGame, "trivia", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId, "Daily Trivia started!", ct);

        return Ok(new { state = publicState });
    }

    // ----------------------------
    // Answer
    // POST /rooms/{roomRef}/games/trivia/answer
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/trivia/answer")]
    public async Task<IActionResult> Answer(string roomRef, [FromBody] TriviaAnswerRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia)
            return BadRequest(new { error = "Trivia is not the active game." });

        if (room.GameState is not TriviaRoundState round || !round.Active || round.Current is null)
            return BadRequest(new { error = "No active trivia question." });

        if (round.Revealed)
            return BadRequest(new { error = "Question already revealed." });

        var userId = (req.UserId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "UserId required." });

        var choice = (req.Choice ?? "").Trim().ToUpperInvariant();
        var idx = choice switch { "A" => 0, "B" => 1, "C" => 2, "D" => 3, _ => -1 };
        if (idx < 0)
            return BadRequest(new { error = "Choice must be A, B, C or D." });

        if (round.Current.ShuffledAnswers.Count < 4 || idx >= round.Current.ShuffledAnswers.Count)
            return BadRequest(new { error = "Invalid answers mapping." });

        // Positional records are init-only, so update via a new state.
        // Clone collections to avoid mutating shared references.
        var answered = new HashSet<string>(round.AnsweredThisQuestionUserIds);
        if (!answered.Add(userId))
            return BadRequest(new { error = "You already answered this question." });

        var chosen = round.Current.ShuffledAnswers[idx];
        var correct = string.Equals(chosen, round.Current.CorrectAnswer, StringComparison.Ordinal);

        var scores = new Dictionary<string, int>(round.ScoresByUserId);
        if (correct)
            scores[userId] = scores.GetValueOrDefault(userId) + 1;

        var nextRound = round with
        {
            ScoresByUserId = scores,
            AnsweredThisQuestionUserIds = answered
        };

        var updated = room with
        {
            GameState = nextRound,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = TriviaPublic.From((TriviaRoundState)updated.GameState!);

        await BroadcastAsync(roomId, updated.ActiveGame, "trivia", publicState, new
        {
            type = "answer",
            userId,
            choice,
            correct
        }, ct);

        if (correct)
            await ToastAsync(roomId, "✅ Correct!", ct);

        return Ok(new { state = publicState, correct });
    }

    // ----------------------------
    // Reveal
    // POST /rooms/{roomRef}/games/trivia/reveal
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/trivia/reveal")]
    public async Task<IActionResult> Reveal(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia)
            return BadRequest(new { error = "Trivia is not the active game." });

        if (room.GameState is not TriviaRoundState round || !round.Active || round.Current is null)
            return BadRequest(new { error = "No active trivia question." });

        var nextRound = round with { Revealed = true };

        var updated = room with
        {
            GameState = nextRound,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = TriviaPublic.From((TriviaRoundState)updated.GameState!);

        await BroadcastAsync(roomId, updated.ActiveGame, "trivia", publicState, new { type = "reveal" }, ct);
        await ToastAsync(roomId, "Answer revealed!", ct);

        return Ok(new { state = publicState });
    }

    // ----------------------------
    // Stop
    // POST /rooms/{roomRef}/games/trivia/stop
    // ----------------------------
    [HttpPost("/rooms/{roomRef}/games/trivia/stop")]
    public async Task<IActionResult> Stop(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var prev = room.GameState as TriviaRoundState;

        // Keep scores if you want (matches "carry forward" behaviour).
        var stopped = new TriviaRoundState(
            Active: false,
            Current: null,
            AskedAtUtc: null,
            Revealed: false,
            ScoresByUserId: prev?.ScoresByUserId ?? [],
            AnsweredThisQuestionUserIds: []
        );

        var updated = room with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = TriviaPublic.From(stopped);

        await BroadcastAsync(roomId, GameType.Trivia, "trivia", publicState, new { type = "stop" }, ct);
        await ToastAsync(roomId, "Trivia stopped.", ct);

        return Ok(new { ok = true });
    }

    // ----------------------------
    // Status
    // GET /rooms/{roomRef}/games/trivia/status
    // ----------------------------
    [HttpGet("/rooms/{roomRef}/games/trivia/status")]
    public async Task<IActionResult> Status(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (_, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia || room.GameState is not TriviaRoundState round)
            return Ok(new { state = new { active = false } });

        return Ok(new { state = TriviaPublic.From(round) });
    }

    // ----------------------------
    // Public projection (Contexto-style)
    // ----------------------------
    internal static class TriviaPublic
    {
        public static object From(TriviaRoundState cs)
        {
            if (cs.Current is null)
                return new { active = false };

            var answers = cs.Current.ShuffledAnswers
                .Select((text, i) => new { key = "ABCD"[i].ToString(), text });

            return new
            {
                active = cs.Active,
                revealed = cs.Revealed,
                askedAtUtc = cs.AskedAtUtc,
                category = cs.Current.Category,
                difficulty = cs.Current.Difficulty,
                question = cs.Current.Question,
                answers,

                correct = cs.Revealed ? cs.Current.CorrectAnswer : null,
                correctKey = cs.Revealed ? GetCorrectKey(cs.Current) : null,

                scores = cs.ScoresByUserId
            };
        }

        private static string? GetCorrectKey(TriviaQuestion q)
        {
            var idx = q.ShuffledAnswers.FindIndex(a => a == q.CorrectAnswer);
            return idx >= 0 ? "ABCD"[idx].ToString() : null;
        }
    }
}
