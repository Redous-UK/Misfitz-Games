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
                new { id = "contexto", name = "Contexto", description = "Guess the secret word. Chat submits guesses.", enabled = true },
                new { id = "deal_or_no_deal", name = "Deal or No Deal", description = "Coming soon.", enabled = false },
                new {id = "hangman", name = "Hangman", description = "Coming soon.", enabled = false },
                new {id = "trivia", name = "Daily Trivia", description = "Coming soon.", enabled = false },
                new {id = "21", name = "21", description = "Coming soon.", enabled = false },
                new { id = "higher_or_lower", name = "Higher or Lower", description = "Coming soon.", enabled = false }
            }
        });
}