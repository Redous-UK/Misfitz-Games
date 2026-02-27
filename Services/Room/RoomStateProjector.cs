using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Misfitz_Games.Controllers;
using Misfitz_Games.Controllers.Games;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Games;
using Misfitz_Games.Services.Games.Hangman;
using Misfitz_Games.Services.Games.HigherLower;
using Misfitz_Games.Services.Games.RiddleMeThis;
using Misfitz_Games.Services.Games.Trivia;
using System.Text.Json;

namespace Misfitz_Games.Services.Room;

public static class RoomStateProjector
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ILogger Logger { get; set; } = NullLogger.Instance;

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
        GameType.Contexto       => ("contexto", ProjectContexto(room.GameState, room)),
        GameType.Hangman        => ("hangman", ProjectHangman(room.GameState, room)),
        GameType.Trivia         => ("trivia", ProjectTrivia(room.GameState, room)),
        GameType.Deal           => ("deal", ProjectPlaceholder("deal")),
        GameType.HigherLower    => ("higher_lower", ProjectHigherLower(room.GameState, room)),
        GameType.RiddleMeThis   => ("riddle_me_this", ProjectRiddleMeThis(room.GameState, room)),
        GameType.None           => ("none", ProjectNone()),
        _                       => ("none", ProjectUnknown(room))
    };

    private static object ProjectNone() => new { };

    private static object ProjectUnknown(RoomState room)
    {
        Logger.LogError(
            "RoomStateProjector: Failed to project game state. RoomId={RoomId}, ActiveGame={ActiveGame}, GameStateType={Type}",
            room.RoomId,
            room.ActiveGame,
            room.GameState?.GetType().Name ?? "null"
        );

        return new { error = "invalid_game_state" };
    }

    private static object ProjectPlaceholder(string id) => new { active = false, isActive = false, comingSoon = true, id };

    private static object ProjectContexto(object? gameState, RoomState room)
    {
        if (gameState is ContextoState cs)
            return ContextoPublic.From(cs);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<ContextoState>(JsonOpts);
            if (typed is not null)
                return ContextoPublic.From(typed);
        }

        return ProjectUnknown(room);
    }

    private static object ProjectHangman(object? gameState, RoomState room)
    {
        if (gameState is HangmanState hs)
            return HangmanView.PublicView(hs);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<HangmanState>(JsonOpts);
            if (typed is not null)
                return HangmanView.PublicView(typed);
        }

        return ProjectUnknown(room);
    }

    private static object ProjectTrivia(object? gameState, RoomState room)
    {
        if (gameState is TriviaRoundState round)
            return TriviaView.PublicView(round);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<TriviaRoundState>(JsonOpts);
            if (typed is not null)
                return TriviaView.PublicView(typed);
        }

        return ProjectUnknown(room);
    }

    private static object ProjectHigherLower(object? gameState, RoomState room)
    {
        if (gameState is HigherLowerState st)
            return HigherLowerView.PublicView(st);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<HigherLowerState>(JsonOpts);
            if (typed is not null)
                return HigherLowerView.PublicView(typed);
        }

        return ProjectUnknown(room);
    }

    private static object ProjectRiddleMeThis(object? gameState, RoomState room)
    {
        if (gameState is RiddleMeThisState st)
            return RiddleMeThisView.PublicView(st);

        if (gameState is JsonElement je)
        {
            var typed = je.Deserialize<RiddleMeThisState>(JsonOpts);
            if (typed is not null)
                return RiddleMeThisView.PublicView(typed);
        }

        return ProjectUnknown(room);
    }
}
