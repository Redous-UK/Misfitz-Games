using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.Trivia;

public static class TriviaView
{
    // Public projection the browser/overlay consumes
    public static object PublicView(TriviaRoundState cs)
    {
        if (cs.Current is null)
            return new { active = false, isActive = false };

        var answers = (cs.Current.ShuffledAnswers ?? [])
            .Select((text, i) => new { key = "ABCD"[i].ToString(), text });

        return new
        {
            // Keep both for compatibility while you unify UI
            active = cs.Active,
            isActive = cs.Active,

            revealed = cs.Revealed,
            askedAtUtc = cs.AskedAtUtc,

            category = cs.Current.Category,
            difficulty = cs.Current.Difficulty,
            question = cs.Current.Question,
            answers,

            correct = cs.Revealed ? cs.Current.CorrectAnswer : null,
            correctKey = cs.Revealed ? GetCorrectKey(cs.Current) : null,

            scores = cs.ScoresByUserId ?? [],

            // Timers / automation (these exist on your record with defaults)
            endsAtUtc = cs.EndsAtUtc,
            nextStartsAtUtc = cs.NextStartsAtUtc,
            autoNext = cs.AutoNext,
            autoNextDelaySeconds = cs.AutoNextDelaySeconds,
            roundSeconds = cs.RoundSeconds,

            // Useful for UI timer bar/badge
            secondsLeft = cs.EndsAtUtc is null
                ? (int?)null
                : (int)Math.Max(0, (cs.EndsAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds)
        };
    }


    private static string? GetCorrectKey(TriviaQuestion q)
    {
        var answers = q.ShuffledAnswers ?? [];
        var idx = answers.FindIndex(a => a == q.CorrectAnswer);
        return idx >= 0 ? "ABCD"[idx].ToString() : null;
    }
}
