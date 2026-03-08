using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.RiddleMeThis;

public static class RiddleMeThisView
{
    public static object PublicView(RiddleMeThisState st) => new
    {
        round = st.Round,
        category = st.Category,
        riddle = st.Riddle,
        isSolved = st.IsSolved,
        solvedByUserId = st.SolvedByUserId,
        startedAtUtc = st.StartedAtUtc,
        solvedAtUtc = st.SolvedAtUtc,
        recentGuesses = st.RecentGuesses.TakeLast(10).Select(g => new
        {
            userId = g.UserId,
            guess = g.Guess,
            isCorrect = g.IsCorrect,
            atUtc = g.AtUtc
        })
    };
}