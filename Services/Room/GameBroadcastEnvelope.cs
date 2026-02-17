namespace Misfitz_Games.Services.Room;

public static class GameBroadcastEnvelope
{
    public static object Build(
        Guid roomId,
        int activeGame,
        string gameId,
        object state,
        object? lastEvent = null,
        DateTimeOffset? utc = null
    )
        => new
        {
            roomId,
            activeGame, // int enum value
            game = new { id = gameId, state },
            lastEvent,
            utc = utc ?? DateTimeOffset.UtcNow
        };
}