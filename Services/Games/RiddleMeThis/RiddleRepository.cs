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

        return await query
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync(ct);
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

        if (usedIds is { Count: > 0 })
        {
            query = query.Where(x => !usedIds.Contains(x.Id.ToString()));
        }

        return await query
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync(ct);
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