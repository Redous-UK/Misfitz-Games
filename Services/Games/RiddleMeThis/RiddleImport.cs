using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Games;
using System.Net.Http.Json;
using System.Text.Json;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public sealed class RiddleImportService(
    HttpClient http,
    AppDbContext db,
    ILogger<RiddleImportService> log)
{
    public async Task<int> ImportCategoryAsync(string category, CancellationToken ct = default)
    {
        category = (category ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.", nameof(category));

        var url = $"https://riddles-api-eight.vercel.app/{category}";
        var rows = await http.GetFromJsonAsync<List<ApiRiddleDto>>(url, cancellationToken: ct)
                   ?? [];

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

            var acceptableAnswersJson = JsonSerializer.Serialize(new[] { answer });

            db.RiddleCatalogs.Add(new RiddleCatalog
            {
                Id = Guid.NewGuid(),
                Category = string.IsNullOrWhiteSpace(item.Category) ? category : item.Category.Trim().ToLowerInvariant(),
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