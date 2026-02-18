using Microsoft.AspNetCore.Mvc;

namespace Misfitz_Games.Controllers.Games;

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
                new { id = "deal_or_no_deal", name = "Deal or No Deal", description = "Pick cases, eliminate amounts, and decide: Deal… or No Deal?", enabled = true, image = "/assets/Games/deal.png"},
                new { id = "hangman", name = "Hangman", description = "Guess the hidden word one letter at a time. Too many wrong guesses and it’s game over!", enabled = true, image = "/assets/Games/Hangman/hangman.png"},
                new { id = "trivia", name = "Daily Trivia", description = "Coming soon.", enabled = false, image = "/assets/Games/trivia.png"},
                new { id = "twenty_one", name = "21", description = "Coming soon.", enabled = false, image = "/assets/Games/21.png"},
                new { id = "higher_or_lower", name = "Higher or Lower", description = "Coming soon.", enabled = false, image = "/assets/Games/higher_or_lower.png"},
                new { id = "connect_four", name = "Connect Four", description = "Coming soon.", enabled = false, image = "/assets/Games/connect_four.png"},
                new { id = "pictionary", name = "Pictionary", description = "Coming soon.", enabled = false, image = "/assets/Games/pictionary.png"},
                new { id = "charades", name = "Charades", description = "Coming soon.", enabled = false, image = "/assets/Games/charades.png"},
              //new { id = "family_feud", name = "Family Feud", description = "Coming soon.", enabled = false, image = "/assets/Games/family_feud.png"},
                new { id = "bingo", name = "Bingo", description = "Coming soon.", enabled = false, image = "/assets/Games/bingo.png"},
              //new { id = "roulette", name = "Roulette", description = "Coming soon.", enabled = false, image = "/assets/Games/roulette.png"},
              //new { id = "blackjack", name = "Blackjack", description = "Coming soon.", enabled = false, image = "/assets/Games/blackjack.png"},
              //new { id = "poker", name = "Poker", description = "Coming soon.", enabled = false, image = "/assets/Games/poker.png"},
              //new { id = "slots", name = "Slots", description = "Coming soon.", enabled = false, image = "/assets/Games/slots.png"},
              //new { id = "minesweeper", name = "Minesweeper", description = "Coming soon.", enabled = false, image = "/assets/Games/minesweeper.png"},
              //new { id = "snake", name = "Snake", description = "Coming soon.", enabled = false, image = "/assets/Games/snake.png"},
              //new { id = "tetris", name = "Tetris", description = "Coming soon.", enabled = false, image = "/assets/Games/tetris.png"},
                new { id = "wordle", name = "Wordle", description = "Coming soon.", enabled = false, image = "/assets/Games/wordle.png"},
              //new { id = "2048", name = "2048", description = "Coming soon.", enabled = false, image = "/assets/Games/2048.png"},
              //new { id = "solitaire", name = "Solitaire", description = "Coming soon.", enabled = false, image = "/assets/Games/solitaire.png"},
              //new { id = "mahjong", name = "Mahjong", description = "Coming soon.", enabled = false, image = "/assets/Games/mahjong.png"},
              //new { id = "clue", name = "Clue", description = "Coming soon.", enabled = false, image = "/assets/Games/clue.png"},
              //new { id = "monopoly", name = "Monopoly", description = "Coming soon.", enabled = false, image = "/assets/Games/monopoly.png"},
              //new { id = "risk", name = "Risk", description = "Coming soon.", enabled = false, image = "/assets/Games/risk.png"},
              //new { id = "catan", name = "Catan", description = "Coming soon.", enabled = false, image = "/assets/Games/catan.png"},
              //new { id = "carcassonne", name = "Carcassonne", description = "Coming soon.", enabled = false, image = "/assets/Games/carcassonne.png"},
              //new { id = "ticket_to_ride", name = "Ticket to Ride", description = "Coming soon.", enabled = false, image = "/assets/Games/ticket_to_ride.png"},
              //new { id = "pandemic", name = "Pandemic", description = "Coming soon.", enabled = false, image = "/assets/Games/pandemic.png"},
              //new { id = "gloomhaven", name = "Gloomhaven", description = "Coming soon.", enabled = false, image = "/assets/Games/gloomhaven.png"},
              //new { id = "dungeons_and_dragons", name = "Dungeons & Dragons", description = "Coming soon.", enabled = false, image = "/assets/Games/dungeons_and_dragons.png"},
            }
        });
}