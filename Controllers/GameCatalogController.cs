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
                new { id = "deal_or_no_deal", name = "Deal or No Deal", description = "Coming soon.", enabled = false }
            }
        });
}