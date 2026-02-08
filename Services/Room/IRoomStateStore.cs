using Misfitz_Games.Models;

namespace Misfitz_Games.Services;

public interface IRoomStateStore
{
    Task SaveRoomAsync(RoomDto room, CancellationToken ct = default);
    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(CancellationToken ct = default);
    Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<RoomState?> GetStateAsync(Guid roomId, CancellationToken ct = default);
    Task SaveStateAsync(RoomState state, CancellationToken ct = default);
    Task<bool> DeleteRoomAsync(Guid roomId, CancellationToken ct = default);
    Task<int> DeleteRoomsOlderThanAsync(DateTimeOffset cutoffUtc, int maxToDelete = 200, CancellationToken ct = default);
    Task<IReadOnlyList<RoomDto>> ListRoomsOlderThanAsync(DateTimeOffset cutoffUtc, int max = 200, CancellationToken ct = default);
    Task<Guid?> ResolveRoomIdAsync(string roomIdOrCode, CancellationToken ct = default);
    Task<bool> TryReserveRoomCodeAsync(string roomCode, Guid roomId, CancellationToken ct = default);
    Task ReleaseRoomCodeAsync(string roomCode, CancellationToken ct = default);
    Task AddToLeaderboardAsync(Guid roomId, IReadOnlyDictionary<string, int> deltaByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<(string userId, double score)>> GetLeaderboardAsync(Guid roomId, int top = 20, CancellationToken ct = default);
    Task IncrementGamesPlayedAsync(Guid roomId, long delta = 1, CancellationToken ct = default);
    Task IncrementGuessesTotalAsync(Guid roomId, long delta = 1, CancellationToken ct = default);
    Task<RoomStatsDto> GetRoomStatsAsync(Guid roomId, CancellationToken ct = default);
    Task ResetRoomStatsAsync(Guid roomId, CancellationToken ct = default);
}