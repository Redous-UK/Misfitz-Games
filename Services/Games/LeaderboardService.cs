using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;

public sealed class LeaderboardService
{
    private readonly AppDbContext _db;

    public LeaderboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddScoreAsync(
        Guid roomId,
        string userId,
        string username,
        GameType gameType,
        int points,
        bool countWin = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var row = await _db.RoomPlayerScores
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == userId);

        if (row == null)
        {
            row = new RoomPlayerScore
            {
                RoomId = roomId,
                UserId = userId,
                Username = username ?? ""
            };

            _db.RoomPlayerScores.Add(row);
        }

        row.Username = string.IsNullOrWhiteSpace(username) ? row.Username : username;
        row.TotalScore += points;
        row.GamesPlayed += 1;
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

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

        if (countWin)
            row.Wins += 1;

        await _db.SaveChangesAsync();
    }
}