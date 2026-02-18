using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.Trivia;

public static class TriviaView
{
    public static object PublicView(TriviaRoundState round)
    {
        if (!round.Active || round.Current is null)
            return new { active = false };

        var answers = round.Current.ShuffledAnswers
            .Select((text, i) => new { key = "ABCD"[i].ToString(), text });

        return new
        {
            active = true,
            revealed = round.Revealed,
            askedAtUtc = round.AskedAtUtc,
            category = round.Current.Category,
            difficulty = round.Current.Difficulty,
            question = round.Current.Question,
            answers,
            // only show correct when revealed (or omit and rely on reveal event)
            correctKey = round.Revealed ? GetCorrectKey(round.Current) : null,
            correct = round.Revealed ? round.Current.CorrectAnswer : null,
            scores = round.ScoresByUserId
        };
    }

    private static string? GetCorrectKey(TriviaQuestion q)
    {
        var idx = q.ShuffledAnswers.FindIndex(a => a == q.CorrectAnswer);
        return (idx >= 0 && idx < 4) ? "ABCD"[idx].ToString() : null;
    }
}