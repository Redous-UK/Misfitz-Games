using Misfitz_Games.Controllers;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Games.Hangman;

namespace Misfitz_Games.Services.Room;

public static class RoomStateProjector
{
    // IMPORTANT: Keep game state public + consistent
    // This is the room-wide "public state" shape clients consume.
    public static object ToPublic(RoomState room)
    {
        var (gameId, gameStatePublic) = ProjectGame(room);

        return new
        {
            roomId = room.RoomId,
            roomName = room.RoomName,
            activeGame = (int)room.ActiveGame,
            updatedAtUtc = room.UpdatedAtUtc,
            players = room.Players ?? [],
            hostUserId = room.HostUserId,

            // Game block is ALWAYS present, even if none
            game = new
            {
                id = gameId,
                state = gameStatePublic
            },

            utc = DateTimeOffset.UtcNow
        };
    }

    private static (string gameId, object state) ProjectGame(RoomState room)
    {
        // Always return a game object, even if inactive
        if (room.ActiveGame == GameType.None || room.GameState is null)
            return ("none", new { isActive = false });

        // Use safe typed extraction (handles JsonElement after persistence)
        switch (room.ActiveGame)
        {
            case GameType.Contexto:
                {
                    if (GameStateJson.TryDeserialize(room.GameState, out ContextoState cs))
                        return ("contexto", ContextoPublic.From(cs));

                    return ("contexto", new { game = "contexto", isActive = false });
                }

            case GameType.Hangman:
                {
                    if (GameStateJson.TryDeserialize(room.GameState, out HangmanState hs))
                        return ("hangman", HangmanView.PublicView(hs));

                    return ("hangman", new { game = "hangman", isActive = false });
                }

            case GameType.Deal:
            default:
                // Placeholder until Deal has a public projector
                return (room.ActiveGame.ToString().ToLowerInvariant(), new { isActive = false });
        }
    }
}
