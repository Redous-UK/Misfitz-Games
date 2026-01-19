using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public static class RoomStateProjector
{
    public static object ToPublic(RoomState state)
    {
        if (state.ActiveGame == GameType.Contexto && state.GameState is ContextoState cs)
        {
            return state with
            {
                GameState = new
                {
                    isActive = cs.IsActive,
                    startedAtUtc = cs.StartedAtUtc,
                    endedAtUtc = cs.EndedAtUtc,
                    recentGuesses = cs.RecentGuesses,
                    scoresByUserId = cs.ScoresByUserId
                }
            };
        }

        return state;
    }
}