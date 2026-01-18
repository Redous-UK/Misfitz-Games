using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public interface IRoomStateStore
{
    Task SaveRoomAsync(RoomDto room, CancellationToken ct = default);
    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);

    Task<RoomState?> GetStateAsync(Guid roomId, CancellationToken ct = default);
    Task SaveStateAsync(RoomState state, CancellationToken ct = default);
}