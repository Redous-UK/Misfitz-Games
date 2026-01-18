using System.Text.Json;
using Misfitz_Games.Models;
using StackExchange.Redis;

namespace Misfitz_Games.Services;

public sealed class RedisRoomStateStore(IConnectionMultiplexer mux) : IRoomStateStore
{
    private readonly IDatabase _db = mux.GetDatabase();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string RoomKey(Guid roomId) => $"room:{roomId:D}:meta";
    private static string RoomsIndexKey => "rooms:index";
    private static string StateKey(Guid roomId) => $"room:{roomId:D}:state";

    public async Task SaveRoomAsync(RoomDto room, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(room, JsonOpts);
        await _db.StringSetAsync(RoomKey(room.RoomId), json).ConfigureAwait(false);
        await _db.SortedSetAddAsync(RoomsIndexKey, room.RoomId.ToString("D"), room.CreatedAtUtc.ToUnixTimeSeconds())
                 .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(CancellationToken ct = default)
    {
        var ids = await _db.SortedSetRangeByRankAsync(RoomsIndexKey, 0, -1, Order.Ascending)
                           .ConfigureAwait(false);

        var results = new List<RoomDto>(ids.Length);
        foreach (var idVal in ids)
        {
            if (!Guid.TryParse(idVal.ToString(), out var id)) continue;
            var room = await GetRoomAsync(id, ct).ConfigureAwait(false);
            if (room is not null) results.Add(room);
        }
        return results;
    }

    public async Task<RoomDto?> GetRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        var json = await _db.StringGetAsync(RoomKey(roomId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<RoomDto>(json!, JsonOpts);
    }

    public async Task<RoomState?> GetStateAsync(Guid roomId, CancellationToken ct = default)
    {
        var json = await _db.StringGetAsync(StateKey(roomId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<RoomState>(json!, JsonOpts);
    }

    public async Task SaveStateAsync(RoomState state, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        await _db.StringSetAsync(StateKey(state.RoomId), json).ConfigureAwait(false);
    }
}