using Microsoft.AspNetCore.Mvc;

namespace Misfitz_Games.Controllers;

[ApiController]
public sealed class GameCatalogController : ControllerBase
{
    [HttpGet("/catalog/games")]
    public IActionResult Games()
        => Ok(new
        {
            ok = true,
            games = new[]
            {
                new { id = "contexto", name = "Contexto", description = "Guess the secret word. Chat submits guesses.", enabled = true, image = "/assets/Games/contexto.png"},
                new { id = "deal_or_no_deal", name = "Deal or No Deal", description = "Coming soon.", enabled = false, image = "/assets/Games/deal.png"},
                new { id = "hangman", name = "Hangman", description = "Coming soon.", enabled = false, image = "/assets/Games/hangman.png"},
                new { id = "trivia", name = "Daily Trivia", description = "Coming soon.", enabled = false, image = "/assets/Games/trivia.png"},
                new { id = "twenty_one", name = "21", description = "Coming soon.", enabled = false, image = "/assets/Games/21.png"},
                new { id = "higher_or_lower", name = "Higher or Lower", description = "Coming soon.", enabled = false, image = "/assets/Games/higher_or_lower.png"}
            }
        });
}