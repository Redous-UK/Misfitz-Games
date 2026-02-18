using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Services.Games.Hangman;

public static class HangmanView
{
    public static string Masked(HangmanState st) =>
        string.IsNullOrWhiteSpace(st.Word)
            ? ""
            : new string([..st.Word.Select(ch =>
                char.IsLetter(ch)
                    ? (st.Guessed.Contains(char.ToUpperInvariant(ch)) ? ch : '_')
                    : ch
            )]);

    public static int WrongCount(HangmanState st) => st.WrongGuesses.Count;

    public static bool IsWin(HangmanState st) =>
        !string.IsNullOrWhiteSpace(st.Word) &&
        Masked(st).Replace("_", "") == st.Word;

    public static bool IsLose(HangmanState st) => WrongCount(st) >= st.MaxWrong;

    public static bool IsActive(HangmanState st) =>
        !string.IsNullOrWhiteSpace(st.Word) && !IsWin(st) && !IsLose(st);

    public static object PublicView(HangmanState st) => new
    {
        game = "hangman",
        masked = Masked(st),
        guessed = st.Guessed.OrderBy(c => c).Select(c => c.ToString()).ToArray(),
        wrong = st.WrongGuesses.ToArray(),
        wrongCount = WrongCount(st),
        maxWrong = st.MaxWrong,
        isWin = IsWin(st),
        isLose = IsLose(st),
        isActive = IsActive(st)
    };
}
