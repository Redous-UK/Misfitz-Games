using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Games;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Games.Trivia;

namespace Misfitz_Games.Services.Room;

public static class RoomStateProjector
{
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
            game = new { id = gameId, state = gameStatePublic },
            utc = DateTimeOffset.UtcNow
        };
    }

    private static (string gameId, object gameStatePublic) ProjectGame(RoomState room) => room.ActiveGame switch
    {
        GameType.Contexto => ("contexto", ProjectContexto(room.GameState)),
        GameType.Hangman => ("hangman", ProjectHangman(room.GameState)),
        GameType.Trivia => ("trivia", ProjectTrivia(room.GameState)),
        GameType.Deal => ("deal", ProjectPlaceholder("deal")),
        _ => ("none", ProjectNone()),
    };

    private static object ProjectNone() => new { active = false, isActive = false };
    private static object ProjectPlaceholder(string id) => new { active = false, isActive = false, comingSoon = true, id };

    private static object ProjectContexto(object? gameState)
    {
        if (gameState is not ContextoState cs)
            return ProjectNone();
        return ContextoPublic.From(cs);
    }
    private static object ProjectHangman(object? gameState)
    {
        if (gameState is not HangmanState hs)
            return ProjectNone();
        return HangmanView.PublicView(hs);
    }
    private static object ProjectTrivia(object? gameState)
    {
        if (gameState is not TriviaRoundState round)
            return ProjectNone();
        return TriviaView.PublicView(round);
    }
}
