using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public sealed class RiddleRepository(AppDbContext db)
{
    public async Task<Riddle?> GetRandomAsync(string? category, CancellationToken ct)
    {
        var q = db.Riddles.AsNoTracking().Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(r => r.Category == category);

        // SQLite-friendly random:
        return await q.OrderBy(_ => Guid.NewGuid()).FirstOrDefaultAsync(ct);
    }

    public async Task<RiddleCatalog?> GetRandomUnusedAsync(
    string? category,
    IReadOnlyCollection<string> usedIds,
    CancellationToken ct = default)
    {
        var query = db.RiddleCatalogs
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

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct)
    {
        return await db.Riddles.AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<long> CreateAsync(Riddle r, CancellationToken ct)
    {
        r.CreatedAtUtc = DateTimeOffset.UtcNow;
        r.UpdatedAtUtc = DateTimeOffset.UtcNow;

        db.Riddles.Add(r);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }
}