using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.HigherLower;

public static class HigherLowerView
{
    public static bool IsActive(HigherLowerState st)
        => st.Status == HigherLowerStatus.InRound;

    public static object PublicView(HigherLowerState st) => new
    {
        game = "higher_lower",
        status = st.Status.ToString().ToLowerInvariant(), // "idle" | "inround" | "revealed" | "finished"

        current = new { label = st.Current.Label },

        // Only show the next card label after a loss reveal.
        revealedNext = st.Status == HigherLowerStatus.Revealed && st.RevealedNext is not null
            ? new { label = st.RevealedNext.Label }
            : null,

        streak = st.Streak,
        bestStreak = st.BestStreak,

        lastChoice = st.LastChoice,
        lastWasCorrect = st.LastWasCorrect,

        isActive = IsActive(st),
        updatedAtUtc = st.UpdatedAtUtc
    };
}
