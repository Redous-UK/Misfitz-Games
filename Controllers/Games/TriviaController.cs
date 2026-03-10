using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.Trivia;
using Misfitz_Games.Services.Room;
using System.Text.Json;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class TriviaController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    TriviaService trivia
) : RoomGameControllerBase(store, bus)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryGetRound(object? gameState, out TriviaRoundState round)
    {
        if (gameState is TriviaRoundState r)
        {
            round = r;
            return true;
        }

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<TriviaRoundState>(JsonOpts);
            if (typed is not null)
            {
                round = typed;
                return true;
            }
        }

        round = default!;
        return false;
    }

    public sealed class TriviaStartRequest
    {
        public string? Difficulty { get; set; }
        public int? RoundSeconds { get; set; }
        public bool? AutoNext { get; set; }
        public int? AutoNextDelaySeconds { get; set; }
    }

    public sealed class TriviaAnswerRequest
    {
        public string? UserId { get; set; }
        public string? Choice { get; set; }
    }

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

        var prev = room.GameState as TriviaRoundState;
        var roundSeconds = (req.RoundSeconds is > 0) ? req.RoundSeconds.Value : 20;
        var autoNext = req.AutoNext ?? false;
        var autoNextDelay = (req.AutoNextDelaySeconds is >= 0) ? req.AutoNextDelaySeconds.Value : 7;

        var now = DateTimeOffset.UtcNow;

        var round = new TriviaRoundState(
            Active: true,
            Current: q,
            AskedAtUtc: now,
            Revealed: false,
            ScoresByUserId: prev?.ScoresByUserId ?? [],
            AnsweredThisQuestionUserIds: [],
            EndsAtUtc: now.AddSeconds(roundSeconds),
            AutoNext: autoNext,
            AutoNextDelaySeconds: autoNextDelay,
            NextStartsAtUtc: null,
            RoundSeconds: roundSeconds
        );

        var updated = room with
        {
            ActiveGame = GameType.Trivia,
            GameState = round,
            UpdatedAtUtc = now
        };

        await SaveRoomStateAsync(roomId, updated, ct);
        await Store.IncrementGamesPlayedAsync(roomId, 1, ct);

        var publicState = TriviaView.PublicView((TriviaRoundState)updated.GameState!);

        await BroadcastAsync(roomId, updated.ActiveGame, "trivia", publicState, new { type = "start" }, ct);
        await ToastAsync(roomId, "Daily Trivia started!", ct);

        return Ok(new { state = publicState });
    }


    [HttpPost("/rooms/{roomRef}/games/trivia/answer")]
    public async Task<IActionResult> Answer(string roomRef, [FromBody] TriviaAnswerRequest req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia)
            return BadRequest(new { error = "Trivia is not the active game." });

        if (!TryGetRound(room.GameState, out var round) || !round.Active || round.Current is null)
            return BadRequest(new { error = "No active trivia question." });

        var difficulty = round.Current.Difficulty;
        var progressed = await TryAutoProgressAsync(roomId, room, round, difficulty, ct);
        (room, round) = progressed;

        if (round.Revealed)
            return BadRequest(new { error = "Question already revealed." });

        var userId = (req.UserId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "UserId required." });

        var choice = (req.Choice ?? "").Trim().ToUpperInvariant();
        var idx = choice switch { "A" => 0, "B" => 1, "C" => 2, "D" => 3, _ => -1 };
        if (idx < 0)
            return BadRequest(new { error = "Choice must be A, B, C or D." });

        var current = round.Current;
        if (current is null)
            return BadRequest(new { error = "No active trivia question." });

        if (current.ShuffledAnswers.Count < 4 || idx >= current.ShuffledAnswers.Count)
            return BadRequest(new { error = "Invalid answers mapping." });

        var answered = new HashSet<string>(round.AnsweredThisQuestionUserIds);
        if (!answered.Add(userId))
            return BadRequest(new { error = "You already answered this question." });

        var chosen = current.ShuffledAnswers[idx];
        var correct = string.Equals(chosen, current.CorrectAnswer, StringComparison.Ordinal);

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

        var publicState = TriviaView.PublicView((TriviaRoundState)updated.GameState!);

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

    [HttpPost("/rooms/{roomRef}/games/trivia/reveal")]
    public async Task<IActionResult> Reveal(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia)
            return BadRequest(new { error = "Trivia is not the active game." });

        if (!TryGetRound(room.GameState, out var round) || !round.Active || round.Current is null)
            return BadRequest(new { error = "No active trivia question." });

        var nextRound = round with { Revealed = true };

        var updated = room with
        {
            GameState = nextRound,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = TriviaView.PublicView((TriviaRoundState)updated.GameState!);

        await BroadcastAsync(roomId, updated.ActiveGame, "trivia", publicState, new { type = "reveal" }, ct);
        await ToastAsync(roomId, "Answer revealed!", ct);

        return Ok(new { state = publicState });
    }

    [HttpPost("/rooms/{roomRef}/games/trivia/stop")]
    public async Task<IActionResult> Stop(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var prev = room.GameState as TriviaRoundState;

        var stopped = new TriviaRoundState(
            Active: false,
            Current: null,
            AskedAtUtc: null,
            Revealed: false,
            ScoresByUserId: prev?.ScoresByUserId ?? [],
            AnsweredThisQuestionUserIds: [],
            EndsAtUtc: null,
            AutoNext: false,
            AutoNextDelaySeconds: 7,
            NextStartsAtUtc: null,
            RoundSeconds: prev?.RoundSeconds ?? 20
        );

        var updated = room with
        {
            ActiveGame = GameType.None,
            GameState = null,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, updated, ct);

        var publicState = TriviaView.PublicView(stopped);

        await BroadcastAsync(roomId, GameType.Trivia, "trivia", publicState, new { type = "stop" }, ct);
        await ToastAsync(roomId, "Trivia stopped.", ct);

        return Ok(new { ok = true });
    }

    [HttpGet("/rooms/{roomRef}/games/trivia/state")]
    [HttpGet("/rooms/{roomRef}/games/trivia/status")]
    public async Task<IActionResult> Status(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (room.ActiveGame != GameType.Trivia || !TryGetRound(room.GameState, out var round))
            return Ok(new { state = new { active = false } });

        var difficulty = round.Current?.Difficulty ?? "easy";

        var progressed = await TryAutoProgressAsync(roomId, room, round, difficulty, ct);
        var (_, updatedRound) = progressed;

        return Ok(new { state = TriviaView.PublicView(updatedRound) });
    }

    private async Task<(RoomState updatedRoom, TriviaRoundState updatedRound)> TryAutoProgressAsync(
        Guid roomId,
        RoomState room,
        TriviaRoundState round,
        string difficulty,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;

        if (round.Active && !round.Revealed && round.EndsAtUtc is not null && now >= round.EndsAtUtc.Value)
        {
            var revealed = round with
            {
                Revealed = true,
                NextStartsAtUtc = round.AutoNext ? now.AddSeconds(round.AutoNextDelaySeconds) : null
            };

            var revealedRoom = room with
            {
                GameState = revealed,
                UpdatedAtUtc = now
            };

            await SaveRoomStateAsync(roomId, revealedRoom, ct);

            var revealPublic = TriviaView.PublicView((TriviaRoundState)revealedRoom.GameState!);
            await BroadcastAsync(roomId, revealedRoom.ActiveGame, "trivia", revealPublic, new { type = "reveal" }, ct);

            room = revealedRoom;
            round = revealed;
        }

        if (round.Active && round.Revealed && round.AutoNext && round.NextStartsAtUtc is not null && now >= round.NextStartsAtUtc.Value)
        {
            var q = await trivia.GetOneAsync(difficulty, ct);
            if (q is null) return (room, round);

            var next = new TriviaRoundState(
                Active: true,
                Current: q,
                AskedAtUtc: now,
                Revealed: false,
                ScoresByUserId: round.ScoresByUserId,
                AnsweredThisQuestionUserIds: [],
                EndsAtUtc: now.AddSeconds(round.RoundSeconds),
                AutoNext: round.AutoNext,
                AutoNextDelaySeconds: round.AutoNextDelaySeconds,
                NextStartsAtUtc: null,
                RoundSeconds: round.RoundSeconds
            );

            var nextRoom = room with
            {
                ActiveGame = GameType.Trivia,
                GameState = next,
                UpdatedAtUtc = now
            };

            await SaveRoomStateAsync(roomId, nextRoom, ct);

            var startPublic = TriviaView.PublicView((TriviaRoundState)nextRoom.GameState!);
            await BroadcastAsync(roomId, nextRoom.ActiveGame, "trivia", startPublic, new { type = "start" }, ct);

            await ToastAsync(roomId, "Next question!", ct);

            return (nextRoom, next);
        }

        return (room, round);
    }
}