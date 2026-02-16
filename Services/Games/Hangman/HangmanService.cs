using Misfitz_Games.Models;

namespace Misfitz_Games.Services.Games.Hangman;

public static class HangmanService
{
    public static HangmanState StartNew(string word, int maxWrong = 6)
    {
        word = (word ?? "").Trim();
        if (string.IsNullOrWhiteSpace(word))
            throw new ArgumentException("Word required", nameof(word));

        return new HangmanState(
            Word: word,
            Guessed: [],
            WrongGuesses: [],
            MaxWrong: maxWrong <= 0 ? 6 : maxWrong
        );
    }

    public static HangmanState ApplyGuess(HangmanState st, string value, out bool correct, out string message)
    {
        correct = false;

        if (!HangmanView.IsActive(st))
        {
            message = "No active round.";
            return st;
        }

        value = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            message = "Enter a guess.";
            return st;
        }

        return value.Length == 1
            ? GuessLetter(st, value[0], out correct, out message)
            : GuessWord(st, value, out correct, out message);
    }

    private static HangmanState GuessLetter(HangmanState st, char letter, out bool correct, out string message)
    {
        correct = false;
        message = "Enter a single letter A-Z.";

        if (!char.IsLetter(letter)) return st;

        var c = char.ToUpperInvariant(letter);

        var guessed = new HashSet<char>(st.Guessed);
        var wrong = new List<string>(st.WrongGuesses);

        if (guessed.Contains(c) || wrong.Contains(c.ToString()))
        {
            message = "Already guessed.";
            return st;
        }

        if (st.Word.ToUpperInvariant().Contains(c))
        {
            guessed.Add(c);
            correct = true;
            message = "Correct!";
        }
        else
        {
            wrong.Add(c.ToString());
            correct = false;
            message = "Wrong!";
        }

        return st with { Guessed = guessed, WrongGuesses = wrong };
    }

    private static HangmanState GuessWord(HangmanState st, string guess, out bool correct, out string message)
    {
        correct = false;
        message = "Enter a word.";

        guess = (guess ?? "").Trim();
        if (guess.Length == 0) return st;

        if (string.Equals(guess, st.Word, StringComparison.OrdinalIgnoreCase))
        {
            var guessed = new HashSet<char>(st.Guessed);
            foreach (var ch in st.Word.Where(char.IsLetter))
                guessed.Add(char.ToUpperInvariant(ch));

            correct = true;
            message = "Solved!";
            return st with { Guessed = guessed };
        }

        // Penalty: count as 1 wrong “WORD:” entry (easy to tweak later)
        var wrong = new List<string>(st.WrongGuesses) { $"WORD:{guess.ToUpperInvariant()}" };

        correct = false;
        message = "Not it.";
        return st with { WrongGuesses = wrong };
    }
}
