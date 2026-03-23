using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Games;
using System.Text.Json;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public sealed class RiddleImportService(
    HttpClient http,
    AppDbContext db,
    ILogger<RiddleImportService> log)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> ImportCategoryAsync(string category, CancellationToken ct = default)
    {
        category = (category ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.", nameof(category));

        var url = $"https://riddles-api-eight.vercel.app/{category}";

        using var res = await http.GetAsync(url, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);

        log.LogInformation("Riddle import raw response for {Category}: {Raw}", category, raw);

        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("Riddle import failed for {Category}. Status={Status}. Body={Body}",
                category, (int)res.StatusCode, raw);
            return 0;
        }

        var rows = new List<ApiRiddleDto>();

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            log.LogInformation("Riddle import root kind for {Category}: {Kind}", category, root.ValueKind);

            if (root.ValueKind == JsonValueKind.Array)
            {
                rows = JsonSerializer.Deserialize<List<ApiRiddleDto>>(raw, JsonOpts) ?? [];
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("riddle", out _) || root.TryGetProperty("question", out _))
                {
                    var one = JsonSerializer.Deserialize<ApiRiddleDto>(raw, JsonOpts);
                    if (one is not null)
                        rows.Add(one);
                }
                else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    rows = JsonSerializer.Deserialize<List<ApiRiddleDto>>(dataEl.GetRawText(), JsonOpts) ?? [];
                }
                else if (root.TryGetProperty("riddles", out var riddlesEl) && riddlesEl.ValueKind == JsonValueKind.Array)
                {
                    rows = JsonSerializer.Deserialize<List<ApiRiddleDto>>(riddlesEl.GetRawText(), JsonOpts) ?? [];
                }
            }
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Could not parse riddle API response for {Category}. Raw body: {Body}", category, raw);
            return 0;
        }

        log.LogInformation("Parsed {Count} row(s) for category {Category}", rows.Count, category);

        var added = 0;

        foreach (var item in rows)
        {
            var question = (item.Riddle ?? item.Question ?? "").Trim();
            var answer = (item.Answer ?? "").Trim();
            var itemCategory = string.IsNullOrWhiteSpace(item.Category) ? category : item.Category.Trim().ToLowerInvariant();

            log.LogInformation("Parsed item -> Category={Category}, Question={Question}, Answer={Answer}",
                itemCategory, question, answer);

            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
            {
                log.LogInformation("Skipping because question or answer was blank.");
                continue;
            }

            var exists = await db.RiddleCatalogs.AnyAsync(x => x.Question == question, ct);

            if (exists)
            {
                log.LogInformation("Skipping duplicate question: {Question}", question);
                continue;
            }

            var acceptableAnswersJson = JsonSerializer.Serialize(new[] { answer }, JsonOpts);

            db.RiddleCatalogs.Add(new RiddleCatalog
            {
                Id = Guid.NewGuid(),
                Category = itemCategory,
                Difficulty = "easy",
                Question = question,
                Answer = answer,
                AcceptableAnswersJson = acceptableAnswersJson,
                HintsJson = "[]",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            added++;
            log.LogInformation("Queued insert for question: {Question}", question);
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Saved {Count} new riddle(s) for category {Category}", added, category);
        }
        else
        {
            log.LogInformation("No riddles saved for category {Category}", category);
        }

        return added;
    }
}

public sealed class ApiRiddleDto
{
    public string? Category { get; set; }
    public string? Riddle { get; set; }
    public string? Question { get; set; }
    public string? Answer { get; set; }
}