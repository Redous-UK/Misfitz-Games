using System;
using System.Collections.Generic;
using System.Linq;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;

public sealed class RoomOverlayService
{
    public RoomOverlayDto Build(RoomState room)
    {
        if (room == null)
            return ProjectMissing();

        return room.ActiveGame switch
        {
            GameType.None => ProjectNone(room),
            GameType.Contexto => ProjectContexto(room),
            GameType.Deal => ProjectDeal(room),
            GameType.Hangman => ProjectHangman(room),
            GameType.Trivia => ProjectTrivia(room),
            GameType.HigherLower => ProjectHigherLower(room),
            GameType.RiddleMeThis => ProjectRiddleMeThis(room),
            _ => ProjectUnknown(room)
        };
    }

    private static RoomOverlayDto ProjectMissing()
    {
        return new RoomOverlayDto
        {
            RoomCode = "--",
            Title = "Misfitz Gaming",
            Game = "None",
            Status = "Offline",
            Message = "Room not found.",
            Players = new List<OverlayPlayerDto>(),
            Meta = new Dictionary<string, object?>()
        };
    }

    private RoomOverlayDto ProjectNone(RoomState room)
    {
        return CreateBase(
            room,
            game: "None",
            status: "Waiting",
            message: "Room is idle. Waiting for the next game.");
    }

    private RoomOverlayDto ProjectUnknown(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: room.ActiveGame.ToString(),
            status: "Unknown",
            message: "An unsupported game is active in this room.");

        return dto;
    }

    private RoomOverlayDto ProjectContexto(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "Contexto",
            status: "Live",
            message: "Contexto round in progress.");

        if (room.GameState is ContextoState contexto)
        {
            dto.Status = contexto.IsActive ? "Live" : "Finished";

            var lastGuess = contexto.RecentGuesses?
                .OrderByDescending(x => x.TsUtc)
                .FirstOrDefault();

            if (lastGuess != null)
            {
                dto.Message = lastGuess.IsWinner
                    ? $"{lastGuess.Username} found the word!"
                    : $"Best recent guess: {lastGuess.Guess} ({lastGuess.Percentage}%)";

                dto.Meta["lastGuess"] = lastGuess.Guess;
                dto.Meta["lastGuessPercent"] = lastGuess.Percentage;
                dto.Meta["lastGuessRank"] = lastGuess.RankOrScore;
            }
            else
            {
                dto.Message = "Guess the hidden word.";
            }

            dto.Meta["recentGuesses"] = contexto.RecentGuesses?.Count ?? 0;
            dto.Meta["startedAtUtc"] = contexto.StartedAtUtc;

            if (contexto.EndedAtUtc.HasValue)
                dto.Meta["endedAtUtc"] = contexto.EndedAtUtc.Value;

            if (contexto.ScoresByUserId != null && contexto.ScoresByUserId.Count > 0)
                dto.Players = ProjectPlayersFromScoreMap(room, contexto.ScoresByUserId);
        }

        return dto;
    }

    private RoomOverlayDto ProjectDeal(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "Deal",
            status: "Live",
            message: "Deal or No Deal in progress.");

        // No concrete model supplied yet, so keep this safe and generic.
        if (room.GameState != null)
        {
            dto.Meta["stateType"] = room.GameState.GetType().Name;
            dto.Message = "Deal or No Deal is active.";
        }

        return dto;
    }

    private RoomOverlayDto ProjectHangman(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "Hangman",
            status: "Live",
            message: "Hangman round in progress.");

        if (room.GameState is HangmanState hangman)
        {
            var maskedWord = BuildMaskedWord(hangman.Word, hangman.Guessed);
            dto.Message = maskedWord;
            dto.Meta["wrongGuesses"] = hangman.WrongGuesses?.Count ?? 0;
            dto.Meta["maxWrongGuesses"] = hangman.MaxWrong;

            var wrong = hangman.WrongGuesses ?? new List<string>();
            if (wrong.Count > 0)
                dto.Meta["misses"] = string.Join(", ", wrong);

            if (IsHangmanSolved(hangman.Word, hangman.Guessed))
                dto.Status = "Solved";
            else if ((hangman.WrongGuesses?.Count ?? 0) >= hangman.MaxWrong)
                dto.Status = "Failed";
        }

        return dto;
    }

    private RoomOverlayDto ProjectTrivia(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "Trivia",
            status: "Live",
            message: "Trivia is in progress.");

        if (room.GameState is TriviaRoundState trivia)
        {
            dto.Status = trivia.Active ? "Live" : (trivia.Revealed ? "Revealed" : "Waiting");

            if (trivia.Current != null)
            {
                dto.Message = trivia.Current.Question;
                dto.Meta["category"] = trivia.Current.Category;
                dto.Meta["difficulty"] = trivia.Current.Difficulty;
                dto.Meta["answerOptions"] = trivia.Current.ShuffledAnswers?.Count ?? 0;
            }
            else
            {
                dto.Message = "Waiting for the next trivia question.";
            }

            dto.Meta["revealed"] = trivia.Revealed;
            dto.Meta["answeredCount"] = trivia.AnsweredThisQuestionUserIds?.Count ?? 0;
            dto.Meta["roundSeconds"] = trivia.RoundSeconds;

            if (trivia.AskedAtUtc.HasValue)
                dto.Meta["askedAtUtc"] = trivia.AskedAtUtc.Value;

            if (trivia.EndsAtUtc.HasValue)
            {
                dto.Meta["endsAtUtc"] = trivia.EndsAtUtc.Value;
                var remaining = (int)Math.Ceiling((trivia.EndsAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds);
                dto.Meta["timeRemaining"] = Math.Max(0, remaining);
            }

            if (trivia.AutoNext)
                dto.Meta["autoNext"] = true;

            if (trivia.NextStartsAtUtc.HasValue)
                dto.Meta["nextStartsAtUtc"] = trivia.NextStartsAtUtc.Value;

            if (trivia.ScoresByUserId != null && trivia.ScoresByUserId.Count > 0)
                dto.Players = ProjectPlayersFromScoreMap(room, trivia.ScoresByUserId);
        }

        return dto;
    }

    private RoomOverlayDto ProjectHigherLower(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "HigherLower",
            status: "Live",
            message: "Higher or Lower in progress.");

        if (room.GameState is HigherLowerState higherLower)
        {
            dto.Status = higherLower.Status.ToString();

            if (!string.IsNullOrWhiteSpace(higherLower.Current?.Label))
                dto.Message = $"Current card: {higherLower.Current.Label}";

            if (higherLower.Current != null)
                dto.Meta["currentValue"] = higherLower.Current.Value;

            if (higherLower.RevealedNext != null)
            {
                dto.Meta["revealedNext"] = higherLower.RevealedNext.Label;
                dto.Meta["revealedNextValue"] = higherLower.RevealedNext.Value;
            }

            dto.Meta["streak"] = higherLower.Streak;
            dto.Meta["bestStreak"] = higherLower.BestStreak;

            if (!string.IsNullOrWhiteSpace(higherLower.LastChoice))
                dto.Meta["lastChoice"] = higherLower.LastChoice;

            if (higherLower.LastWasCorrect.HasValue)
                dto.Meta["lastWasCorrect"] = higherLower.LastWasCorrect.Value;

            dto.Meta["updatedAtUtc"] = higherLower.UpdatedAtUtc;
        }

        return dto;
    }

    private RoomOverlayDto ProjectRiddleMeThis(RoomState room)
    {
        var dto = CreateBase(
            room,
            game: "RiddleMeThis",
            status: "Live",
            message: "Riddle round in progress.");

        if (room.GameState is RiddleMeThisState riddle)
        {
            dto.Status = riddle.IsSolved ? "Solved" : "Live";
            dto.Message = riddle.Riddle;
            dto.Meta["round"] = riddle.Round;
            dto.Meta["category"] = riddle.Category;
            dto.Meta["isSolved"] = riddle.IsSolved;
            dto.Meta["guessCount"] = riddle.RecentGuesses?.Count ?? 0;
            dto.Meta["startedAtUtc"] = riddle.StartedAtUtc;

            if (riddle.SolvedAtUtc.HasValue)
                dto.Meta["solvedAtUtc"] = riddle.SolvedAtUtc.Value;

            if (!string.IsNullOrWhiteSpace(riddle.SolvedByUserId))
                dto.Meta["solvedByUserId"] = riddle.SolvedByUserId;
        }

        return dto;
    }

    private RoomOverlayDto CreateBase(RoomState room, string game, string status, string message)
    {
        return new RoomOverlayDto
        {
            RoomCode = room.RoomName ?? string.Empty,
            Title = "Misfitz Gaming",
            Game = game,
            Status = status,
            Message = message,
            Players = ProjectPlayers(room),
            Meta = new Dictionary<string, object?>
            {
                ["playerCount"] = room.Players?.Count ?? 0,
                ["updatedAtUtc"] = room.UpdatedAtUtc
            }
        };
    }

    private List<OverlayPlayerDto> ProjectPlayers(RoomState room)
    {
        var players = room.Players ?? new List<PlayerPresence>();

        return players
            .Select(p => new OverlayPlayerDto
            {
                UserId = p.UserId ?? string.Empty,
                Username = string.IsNullOrWhiteSpace(p.Name) ? "Player" : p.Name,
                //Score = GetPlayerScore(p),
                IsHost = string.Equals(p.UserId, room.HostUserId, StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Username)
            .ToList();
    }

    private List<OverlayPlayerDto> ProjectPlayersFromScoreMap(
        RoomState room,
        IReadOnlyDictionary<string, int> scores)
    {
        var players = room.Players ?? new List<PlayerPresence>();

        return players
            .Select(p => new OverlayPlayerDto
            {
                UserId = p.UserId ?? string.Empty,
                Username = string.IsNullOrWhiteSpace(p.Name) ? "Player" : p.Name,
                Score = scores.TryGetValue(p.UserId ?? string.Empty, out var score) ? score : 0,
                IsHost = string.Equals(p.UserId, room.HostUserId, StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Username)
            .ToList();
    }

    //private static int GetPlayerScore(PlayerPresence p)
    //{
        // Adjust this if PlayerPresence uses Points/TotalScore/etc.
       // return p.Score;
  //  }

    private static string BuildMaskedWord(string word, HashSet<char> guessed)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        guessed ??= new HashSet<char>();

        return string.Join(" ", word.Select(ch =>
        {
            if (!char.IsLetter(ch))
                return ch.ToString();

            return guessed.Contains(char.ToUpperInvariant(ch)) || guessed.Contains(char.ToLowerInvariant(ch))
                ? ch.ToString()
                : "_";
        }));
    }

    private static bool IsHangmanSolved(string word, HashSet<char> guessed)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        guessed ??= new HashSet<char>();

        foreach (var ch in word)
        {
            if (!char.IsLetter(ch))
                continue;

            if (!guessed.Contains(char.ToUpperInvariant(ch)) && !guessed.Contains(char.ToLowerInvariant(ch)))
                return false;
        }

        return true;
    }
}
