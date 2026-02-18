using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Games;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Games.Trivia;
using System.Text.Json;

namespace Misfitz_Games.Services.Room;

public static class RoomStateProjector
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
        if (gameState is ContextoState cs)
            return ContextoPublic.From(cs);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<ContextoState>(JsonOpts);
            if (typed is not null)
                return ContextoPublic.From(typed);
        }

        return ProjectNone();
    }

    private static object ProjectHangman(object? gameState)
    {
        if (gameState is HangmanState hs)
            return HangmanView.PublicView(hs);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<HangmanState>(JsonOpts);
            if (typed is not null)
                return HangmanView.PublicView(typed);
        }

        return ProjectNone();
    }

    private static object ProjectTrivia(object? gameState)
    {
        if (gameState is TriviaRoundState round)
            return TriviaView.PublicView(round);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<TriviaRoundState>(JsonOpts);
            if (typed is not null)
                return TriviaView.PublicView(typed);
        }

        return ProjectNone();
    }
}
