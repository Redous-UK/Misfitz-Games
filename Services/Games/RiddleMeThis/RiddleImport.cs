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

            if (root.ValueKind == JsonValueKind.Array)
            {
                rows = JsonSerializer.Deserialize<List<ApiRiddleDto>>(raw, JsonOpts) ?? [];
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Single riddle object
                if (root.TryGetProperty("riddle", out _) || root.TryGetProperty("question", out _))
                {
                    var one = JsonSerializer.Deserialize<ApiRiddleDto>(raw, JsonOpts);
                    if (one is not null)
                        rows.Add(one);
                }
                // Wrapped array shapes
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
            log.LogWarning(ex,
                "Could not parse riddle API response for {Category}. Raw body: {Body}",
                category, raw);
            return 0;
        }

        log.LogInformation("Fetched {Count} rows from upstream for category {Category}", rows.Count, category);

        var added = 0;

        foreach (var item in rows)
        {
            var question = (item.Riddle ?? item.Question ?? "").Trim();
            var answer = (item.Answer ?? "").Trim();

            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                continue;

            var exists = await db.RiddleCatalogs
                .AnyAsync(x => x.Question == question, ct);

            if (exists)
                continue;

            var acceptableAnswersJson = JsonSerializer.Serialize(new[] { answer }, JsonOpts);

            db.RiddleCatalogs.Add(new RiddleCatalog
            {
                Id = Guid.NewGuid(),
                Category = string.IsNullOrWhiteSpace(item.Category)
                    ? category
                    : item.Category.Trim().ToLowerInvariant(),
                Difficulty = "easy",
                Question = question,
                Answer = answer,
                AcceptableAnswersJson = acceptableAnswersJson,
                HintsJson = "[]",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);

        log.LogInformation("Imported {Count} riddles for category {Category}", added, category);
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