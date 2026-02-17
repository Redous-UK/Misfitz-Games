namespace Misfitz_Games.Services.Room;

public sealed class RoomGameBroadcaster(RoomBroadcastService broadcaster)
{
    public Task BroadcastGameStateAsync(
        Guid roomId,
        int activeGame,
        string gameId,
        object publicState,
        object? lastEvent = null,
        CancellationToken ct = default)
    {
        var payload = GameBroadcastEnvelope.Build(
            roomId: roomId,
            activeGame: activeGame,
            gameId: gameId,
            state: publicState,
            lastEvent: lastEvent
        );

        return broadcaster.BroadcastStateAsync(roomId, payload, ct);
    }

    public Task ToastAsync(Guid roomId, string message, CancellationToken ct = default)
        => broadcaster.ToastAsync(roomId, message, ct);
}
