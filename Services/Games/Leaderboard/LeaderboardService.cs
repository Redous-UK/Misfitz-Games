using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public sealed class LeaderboardService(AppDbContext db)
{
    public async Task AddScoreAsync(
        Guid roomId,
        string userId,
        string username,
        GameType gameType,
        int points,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(userId)) return;
        if (points == 0) return;

        var row = await db.RoomPlayerScores
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId, ct);

        if (row is null)
        {
            row = new RoomPlayerScore
            {
                RoomId = roomId,
                UserId = userId.Trim(),
                Username = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim()
            };

            db.RoomPlayerScores.Add(row);
        }
        else if (!string.IsNullOrWhiteSpace(username))
        {
            row.Username = username.Trim();
        }

        row.TotalScore += points;

        switch (gameType)
        {
            case GameType.Trivia:
                row.TriviaScore += points;
                break;
            case GameType.Hangman:
                row.HangmanScore += points;
                break;
            case GameType.HigherLower:
                row.HigherLowerScore += points;
                break;
            case GameType.RiddleMeThis:
                row.RiddleScore += points;
                break;
            case GameType.Contexto:
                row.ContextoScore += points;
                break;
            case GameType.Deal:
                row.DealScore += points;
                break;
        }

        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkPlayedAsync(
        Guid roomId,
        string userId,
        string username,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(userId)) return;

        var row = await db.RoomPlayerScores
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId, ct);

        if (row is null)
        {
            row = new RoomPlayerScore
            {
                RoomId = roomId,
                UserId = userId.Trim(),
                Username = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim()
            };

            db.RoomPlayerScores.Add(row);
        }
        else if (!string.IsNullOrWhiteSpace(username))
        {
            row.Username = username.Trim();
        }

        row.GamesPlayed += 1;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkWinAsync(
        Guid roomId,
        string userId,
        string username,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(userId)) return;

        var row = await db.RoomPlayerScores
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId, ct);

        if (row is null)
        {
            row = new RoomPlayerScore
            {
                RoomId = roomId,
                UserId = userId.Trim(),
                Username = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim()
            };

            db.RoomPlayerScores.Add(row);
        }
        else if (!string.IsNullOrWhiteSpace(username))
        {
            row.Username = username.Trim();
        }

        row.Wins += 1;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}