using Misfitz_Games.Models.Games;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Misfitz_Games.Services.Games.Trivia;

public sealed class TriviaService(HttpClient http)
{
    private sealed class OpenTdbResponse
    {
        [JsonPropertyName("response_code")]
        public int ResponseCode { get; set; }

        [JsonPropertyName("results")]
        public List<OpenTdbQ> Results { get; set; } = [];
    }

    private sealed class OpenTdbQ
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = "";

        [JsonPropertyName("question")]
        public string Question { get; set; } = "";

        [JsonPropertyName("correct_answer")]
        public string CorrectAnswer { get; set; } = "";

        [JsonPropertyName("incorrect_answers")]
        public List<string> IncorrectAnswers { get; set; } = [];

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    public async Task<TriviaQuestion?> GetOneAsync(string difficulty, CancellationToken ct)
    {
        var diff = string.IsNullOrWhiteSpace(difficulty) ? "easy" : difficulty.Trim().ToLowerInvariant();

        var url = $"https://opentdb.com/api.php?amount=1&type=multiple&difficulty={Uri.EscapeDataString(diff)}";
        var json = await http.GetStringAsync(url, ct);

        var data = JsonSerializer.Deserialize<OpenTdbResponse>(json);
        if (data is null || data.ResponseCode != 0 || data.Results.Count == 0)
            return null;

        var q = data.Results[0];

        static string Decode(string s) => WebUtility.HtmlDecode(s ?? "");

        var category = Decode(q.Category);
        var difficultyOut = Decode(q.Difficulty);
        var questionText = Decode(q.Question);
        var correct = Decode(q.CorrectAnswer);

        var incorrect = q.IncorrectAnswers.Select(Decode).ToList();

        // Build shuffled list (A/B/C/D mapping happens in TriviaPublic)
        var shuffled = new List<string>(incorrect) { correct };
        ShuffleInPlace(shuffled);

        // ✅ Positional record: construct once with all values (including ShuffledAnswers)
        return new TriviaQuestion(
            Category: category,
            Difficulty: difficultyOut,
            Question: questionText,
            CorrectAnswer: correct,
            IncorrectAnswers: incorrect,
            ShuffledAnswers: shuffled
        );
    }

    private static void ShuffleInPlace<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
