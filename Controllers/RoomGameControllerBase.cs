using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;

namespace Misfitz_Games.Controllers;

[ApiController]
public abstract class RoomGameControllerBase(
    IRoomStateStore store,
    RoomGameBroadcaster bus
) : ControllerBase
{
    protected IRoomStateStore Store { get; } = store;
    protected RoomGameBroadcaster Bus { get; } = bus;

    protected async Task<Guid?> ResolveRoomIdAsync(string roomIdOrCode, CancellationToken ct)
        => await Store.ResolveRoomIdAsync(roomIdOrCode, ct);

    protected async Task<(Guid roomId, RoomState room)?> LoadRoomStateAsync(string roomIdOrCode, CancellationToken ct)
    {
        var roomId = await ResolveRoomIdAsync(roomIdOrCode, ct);
        if (roomId is null) return null;

        var room = await Store.GetStateAsync(roomId.Value, ct);
        if (room is null) return null;

        return (roomId.Value, room);
    }

    protected static ActionResult RoomNotFound()
        => new NotFoundObjectResult(new { error = "Room not found." });

    protected static ActionResult RoomStateNotFound()
        => new NotFoundObjectResult(new { error = "Room state not found." });

    /// <summary>
    /// Ensures the room is currently running <paramref name="expectedGame"/> and extracts the typed game state.
    /// Works even if GameState was reloaded as JsonElement.
    /// </summary>
    protected static bool TryRequireGameState<TState>(
        RoomState room,
        GameType expectedGame,
        out TState state,
        out ActionResult? error)
    {
        state = default!;
        error = null;

        if (room.ActiveGame != expectedGame)
        {
            error = new BadRequestObjectResult(new { error = $"{expectedGame} is not active in this room." });
            return false;
        }

        if (!GameStateJson.TryDeserialize(room.GameState, out state))
        {
            error = new BadRequestObjectResult(new { error = $"{expectedGame} state could not be loaded." });
            return false;
        }

        return true;
    }

    protected async Task SaveRoomStateAsync(Guid roomId, RoomState updated, CancellationToken ct)
        => await Store.SaveStateAsync(updated, ct);

    protected async Task BroadcastAsync(
        Guid roomId,
        GameType activeGame,
        string gameId,
        object publicState,
        object? lastEvent,
        CancellationToken ct)
    {
        await Bus.BroadcastGameStateAsync(roomId, (int)activeGame, gameId, publicState, lastEvent, ct);
    }

    protected Task ToastAsync(Guid roomId, string message, CancellationToken ct)
        => Bus.ToastAsync(roomId, message, ct);
}
