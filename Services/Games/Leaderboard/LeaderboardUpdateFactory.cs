using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Room;

public static class LeaderboardUpdateFactory
{
    public static LeaderboardUpdate? TryCreate(RoomState? prevState, RoomState nextState)
    {
        if (prevState is null)
            return null;

        return nextState.ActiveGame switch
        {
            GameType.Contexto => TryCreateContexto(prevState, nextState),
            GameType.Trivia => TryCreateTrivia(prevState, nextState),
            GameType.RiddleMeThis => TryCreateRiddle(prevState, nextState),
            _ => null
        };
    }

    private static LeaderboardUpdate? TryCreateContexto(RoomState prevState, RoomState nextState)
    {
        if (prevState.ActiveGame != GameType.Contexto || nextState.ActiveGame != GameType.Contexto)
            return null;

        if (prevState.GameState is not ContextoState prev || nextState.GameState is not ContextoState cur)
            return null;

        var wasEnded = prev.EndedAtUtc is not null;
        var isEnded = cur.EndedAtUtc is not null;

        if (wasEnded || !isEnded)
            return null;

        if (cur.ScoresByUserId is null || cur.ScoresByUserId.Count == 0)
            return null;

        var usernamesByUserId = cur.RecentGuesses?
            .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => string.IsNullOrWhiteSpace(g.Last().Username) ? g.Key : g.Last().Username
            )
            ?? new Dictionary<string, string>();

        foreach (var userId in cur.ScoresByUserId.Keys)
        {
            if (!usernamesByUserId.ContainsKey(userId))
                usernamesByUserId[userId] = userId;
        }

        var winnerUserId = cur.RecentGuesses?
            .OrderByDescending(x => x.TsUtc)
            .FirstOrDefault(x => x.IsWinner)?
            .UserId;

        var roundKey = $"contexto:{nextState.RoomId:D}:{cur.StartedAtUtc:O}";

        return new LeaderboardUpdate(
            RoomId: nextState.RoomId,
            GameType: GameType.Contexto,
            ScoresByUserId: new Dictionary<string, int>(cur.ScoresByUserId),
            UsernamesByUserId: usernamesByUserId,
            WinnerUserId: winnerUserId,
            RoundKey: roundKey
        );
    }

    private static LeaderboardUpdate? TryCreateTrivia(RoomState prevState, RoomState nextState)
    {
        if (prevState.ActiveGame != GameType.Trivia || nextState.ActiveGame != GameType.Trivia)
            return null;

        if (prevState.GameState is not TriviaRoundState prev || nextState.GameState is not TriviaRoundState cur)
            return null;

        var justRevealed = !prev.Revealed && cur.Revealed;
        if (!justRevealed)
            return null;

        if (cur.ScoresByUserId is null || cur.ScoresByUserId.Count == 0)
            return null;

        var usernamesByUserId = (nextState.Players ?? new List<PlayerPresence>())
            .Where(p => !string.IsNullOrWhiteSpace(p.UserId))
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => string.IsNullOrWhiteSpace(g.Last().Name) ? g.Key : g.Last().Name
            );

        foreach (var userId in cur.ScoresByUserId.Keys)
        {
            if (!usernamesByUserId.ContainsKey(userId))
                usernamesByUserId[userId] = userId;
        }

        var winnerUserId = cur.ScoresByUserId
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .First().Key;

        var askedKey = cur.AskedAtUtc?.ToString("O") ?? "no-asked-at";
        var roundKey = $"trivia:{nextState.RoomId:D}:{askedKey}";

        return new LeaderboardUpdate(
            RoomId: nextState.RoomId,
            GameType: GameType.Trivia,
            ScoresByUserId: new Dictionary<string, int>(cur.ScoresByUserId),
            UsernamesByUserId: usernamesByUserId,
            WinnerUserId: winnerUserId,
            RoundKey: roundKey
        );
    }

    private static LeaderboardUpdate? TryCreateRiddle(RoomState prevState, RoomState nextState)
    {
        if (prevState.ActiveGame != GameType.RiddleMeThis || nextState.ActiveGame != GameType.RiddleMeThis)
            return null;

        if (prevState.GameState is not RiddleMeThisState prev || nextState.GameState is not RiddleMeThisState cur)
            return null;

        var justSolved = !prev.IsSolved && cur.IsSolved;
        if (!justSolved)
            return null;

        var winnerUserId = cur.SolvedByUserId;
        if (string.IsNullOrWhiteSpace(winnerUserId))
            return null;

        var scoresByUserId = new Dictionary<string, int>
        {
            [winnerUserId] = 1
        };

        var usernamesByUserId = new Dictionary<string, string>();

        var player = (nextState.Players ?? new List<PlayerPresence>())
            .FirstOrDefault(p => string.Equals(p.UserId, winnerUserId, StringComparison.OrdinalIgnoreCase));

        usernamesByUserId[winnerUserId] =
            !string.IsNullOrWhiteSpace(player?.Name)
                ? player.Name
                : winnerUserId;

        var roundKey = $"riddle:{nextState.RoomId:D}:{cur.RiddleId}:{cur.StartedAtUtc:O}";

        return new LeaderboardUpdate(
            RoomId: nextState.RoomId,
            GameType: GameType.RiddleMeThis,
            ScoresByUserId: scoresByUserId,
            UsernamesByUserId: usernamesByUserId,
            WinnerUserId: winnerUserId,
            RoundKey: roundKey
        );
    }
}