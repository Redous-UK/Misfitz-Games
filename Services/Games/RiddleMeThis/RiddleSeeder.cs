using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public static class RiddleSeeder
{
    public static async Task SeedIfEmptyAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Riddles.AnyAsync(ct)) return;

        db.Riddles.AddRange(
            new Riddle { Category = "Classic", Question = "I speak without a mouth and hear without ears. I have no body, but I come alive with wind. What am I?", Answer = "echo" },
            new Riddle { Category = "Classic", Question = "The more you take, the more you leave behind. What are they?", Answer = "footsteps" },
            new Riddle { Category = "Wordplay", Question = "What has keys but can’t open locks?", Answer = "piano" },
            new Riddle { Category = "Everyday", Question = "What gets wetter the more it dries?", Answer = "towel" }
        );

        await db.SaveChangesAsync(ct);
    }
}