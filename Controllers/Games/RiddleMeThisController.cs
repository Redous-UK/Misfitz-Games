using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Controllers.Rooms;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games.RiddleMeThis;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers.Games;

[ApiController]
public sealed class RiddleMeThisController(
    IRoomStateStore store,
    RoomGameBroadcaster bus,
    RiddleRepository riddles
) : RoomGameControllerBase(store, bus)
{
    private RiddleRepository Riddles { get; } = riddles;

    public sealed record StartReq(string? Category = null);
    public sealed record GuessReq(string Guess);

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/start")]
    [HttpPost("/rooms/{roomRef}/games/riddles/start")]
    public async Task<IActionResult> Start(string roomRef, [FromBody] StartReq? req, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        var pick = await Riddles.GetRandomAsync(req?.Category, ct);
        if (pick is null)
            return BadRequest(new { ok = false, error = "No riddles available (check DB / category / isActive)." });

        var st = new RiddleMeThisState(
            Round: 1,
            RiddleId: pick.Id.ToString(),
            Category: pick.Category,
            Riddle: pick.Question,
            Answer: pick.Answer,
            IsSolved: false,
            SolvedByUserId: null,
            StartedAtUtc: DateTimeOffset.UtcNow,
            SolvedAtUtc: null,
            RecentGuesses: [],
            UsedRiddleIds: [pick.Id.ToString()]
        );

        var next = room with
        {
            ActiveGame = GameType.RiddleMeThis,
            GameState = st,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, next, ct);

        var pub = RiddleMeThisView.PublicView(st);
        await BroadcastAsync(
            roomId,
            GameType.RiddleMeThis,
            "riddle_me_this",
            pub,
            lastEvent: new { type = "started" },
            ct
        );

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/guess")]
    [HttpPost("/rooms/{roomRef}/games/riddles/guess")]
    public async Task<IActionResult> Guess(string roomRef, [FromBody] GuessReq req, CancellationToken ct)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Guess))
            return BadRequest(new { ok = false, error = "Guess is required." });

        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        if (st.IsSolved)
            return Ok(new { ok = true, alreadySolved = true });

        var userId = User?.Identity?.Name ?? "guest";

        static string Norm(string s) => new([.. s.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit)]);
        var isCorrect = Norm(req.Guess) == Norm(st.Answer);

        var guesses = st.RecentGuesses.ToList();
        guesses.Add(new RiddleGuess(userId, req.Guess.Trim(), isCorrect, DateTimeOffset.UtcNow));
        if (guesses.Count > 50)
            guesses.RemoveRange(0, guesses.Count - 50);

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
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
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
    [HttpPost("/rooms/{roomRef}/games/riddles/reveal")]
    public async Task<IActionResult> Reveal(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        await ToastAsync(roomId, $"Answer: {st.Answer}", ct);

        await BroadcastAsync(
            roomId,
            GameType.RiddleMeThis,
            "riddle_me_this",
            RiddleMeThisView.PublicView(st),
            lastEvent: new { type = "reveal" },
            ct
        );

        return Ok(new { ok = true });
    }

    [HttpPost("/rooms/{roomRef}/games/riddle_me_this/next")]
    [HttpPost("/rooms/{roomRef}/games/riddles/next")]
    public async Task<IActionResult> Next(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (roomId, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out var err))
            return err!;

        var usedIds = st.UsedRiddleIds?.ToList() ?? [];

        var pick = await Riddles.GetRandomUnusedAsync(st.Category, usedIds, ct);

        if (pick is null)
        {
            usedIds.Clear();
            pick = await Riddles.GetRandomUnusedAsync(st.Category, usedIds, ct);
        }

        if (pick is null)
            return BadRequest(new { ok = false, error = "No riddles available (check DB / category / isActive)." });

        usedIds.Add(pick.Id.ToString());

        var nextSt = st with
        {
            Round = st.Round + 1,
            RiddleId = pick.Id.ToString(),
            Category = pick.Category,
            Riddle = pick.Question,
            Answer = pick.Answer,
            IsSolved = false,
            SolvedByUserId = null,
            StartedAtUtc = DateTimeOffset.UtcNow,
            SolvedAtUtc = null,
            RecentGuesses = [],
            UsedRiddleIds = usedIds
        };

        var nextRoom = room with
        {
            GameState = nextSt,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveRoomStateAsync(roomId, nextRoom, ct);

        var pub = RiddleMeThisView.PublicView(nextSt);
        await BroadcastAsync(
            roomId,
            GameType.RiddleMeThis,
            "riddle_me_this",
            pub,
            lastEvent: new { type = "next" },
            ct
        );

        return Ok(new { ok = true });
    }

    [HttpGet("/rooms/{roomRef}/games/riddle_me_this/state")]
    [HttpGet("/rooms/{roomRef}/games/riddles/state")]
    public async Task<IActionResult> State(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var (_, room) = loaded.Value;

        if (!TryRequireGameState<RiddleMeThisState>(room, GameType.RiddleMeThis, out var st, out _))
            return Ok(new { ok = true, state = new { game = "riddle_me_this", isActive = false } });

        return Ok(new { ok = true, state = RiddleMeThisView.PublicView(st) });
    }

    [HttpGet("/rooms/{roomRef}/games/riddle_me_this/categories")]
    [HttpGet("/rooms/{roomRef}/games/riddles/categories")]
    public async Task<IActionResult> Categories(string roomRef, CancellationToken ct)
    {
        var loaded = await LoadRoomStateAsync(roomRef, ct);
        if (loaded is null) return RoomNotFound();

        var categories = await Riddles.GetCategoriesAsync(ct);
        return Ok(new { ok = true, categories });
    }

    [HttpPost("/admin/games/riddle_me_this/import/{category}")]
    public async Task<IActionResult> ImportCategory(
        [FromRoute] string category,
        [FromServices] RiddleImportService importer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category))
            return BadRequest(new { ok = false, error = "Category is required." });

        var added = await importer.ImportCategoryAsync(category, ct);

        return Ok(new
        {
            ok = true,
            category,
            added,
            marker = "IMPORT_ENDPOINT_V2"
        });
    }

    [HttpPost("/admin/games/riddle_me_this/import")]
    public async Task<IActionResult> ImportMany(
        [FromBody] string[] categories,
        [FromServices] RiddleImportService importer,
        CancellationToken ct)
    {
        if (categories is null || categories.Length == 0)
            return BadRequest(new { ok = false, error = "At least one category is required." });

        var results = new List<object>();
        var totalAdded = 0;

        foreach (var raw in categories)
        {
            var category = (raw ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(category))
                continue;

            var added = await importer.ImportCategoryAsync(category, ct);
            totalAdded += added;

            results.Add(new
            {
                category,
                added
            });
        }

        return Ok(new
        {
            ok = true,
            totalAdded,
            results
        });
    }

    [HttpGet("/admin/games/riddle_me_this/catalog/count")]
    public async Task<IActionResult> CatalogCount(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var total = await db.RiddleCatalogs.CountAsync(ct);
        var active = await db.RiddleCatalogs.CountAsync(x => x.IsActive, ct);

        var categories = await db.RiddleCatalogs
            .Where(x => x.IsActive)
            .GroupBy(x => x.Category)
            .Select(g => new
            {
                category = g.Key,
                count = g.Count()
            })
            .OrderBy(x => x.category)
            .ToListAsync(ct);

        return Ok(new
        {
            ok = true,
            total,
            active,
            categories
        });
    }

    [HttpGet("/admin/games/riddle_me_this/catalog")]
    public async Task<IActionResult> Catalog(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var items = await db.RiddleCatalogs
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Question)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.Category,
                x.Question,
                x.Answer,
                x.IsActive
            })
            .ToListAsync(ct);

        return Ok(new
        {
            ok = true,
            count = items.Count,
            items
        });
    }

    [HttpGet("/admin/games/riddle_me_this/catalog/find")]
    public async Task<IActionResult> FindCatalogQuestion(
        [FromQuery] string question,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var q = (question ?? "").Trim();

        var matches = await db.RiddleCatalogs
            .Where(x => x.Question == q)
            .Select(x => new
            {
                x.Id,
                x.Category,
                x.Question,
                x.Answer,
                x.IsActive
            })
            .ToListAsync(ct);

        return Ok(new
        {
            ok = true,
            count = matches.Count,
            matches
        });
    }

    [HttpGet("/admin/games/riddle_me_this/import-debug/{category}")]
    public async Task<IActionResult> ImportDebug(
        [FromRoute] string category,
        [FromServices] IHttpClientFactory httpFactory,
        CancellationToken ct)
    {
        var http = httpFactory.CreateClient();
        var url = $"https://riddles-api-eight.vercel.app/{category}";

        using var res = await http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        return Ok(new
        {
            ok = true,
            category,
            status = (int)res.StatusCode,
            contentType = res.Content.Headers.ContentType?.ToString(),
            body = raw
        });
    }

    [HttpPost("/admin/games/riddle_me_this/seed-test")]
    public async Task<IActionResult> SeedTest(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var item = new RiddleCatalog
        {
            Id = Guid.NewGuid(),
            Category = "science",
            Difficulty = "easy",
            Question = "TEST QUESTION " + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Answer = "test",
            AcceptableAnswersJson = "[\"test\"]",
            HintsJson = "[]",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.RiddleCatalogs.Add(item);
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            ok = true,
            item.Id,
            item.Question
        });
    }
}