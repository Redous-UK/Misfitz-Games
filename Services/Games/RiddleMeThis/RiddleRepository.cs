using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public sealed class RiddleRepository(AppDbContext db)
{
    public async Task<RiddleCatalog?> GetRandomAsync(string? category, CancellationToken ct = default)
    {
        var query = db.RiddleCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        var list = await query.ToListAsync(ct);
        if (list.Count == 0) return null;

        return list[Random.Shared.Next(list.Count)];
    }

    public async Task<RiddleCatalog?> GetRandomUnusedAsync(
        string? category,
        IReadOnlyCollection<string> usedIds,
        CancellationToken ct = default)
    {
        var query = db.RiddleCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        var list = await query.ToListAsync(ct);

        if (usedIds is { Count: > 0 })
        {
            list = [..list
                .Where(x => !usedIds.Contains(x.Id.ToString()))];
        }

        if (list.Count == 0) return null;

        return list[Random.Shared.Next(list.Count)];
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await db.RiddleCatalogs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }
}