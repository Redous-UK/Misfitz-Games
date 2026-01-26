using System.Text.Json;
using Misfitz_Games.Models;
using StackExchange.Redis;

namespace Misfitz_Games.Services;

public sealed class RedisRoomStateStore(RedisMuxFactory muxFactory) : IRoomStateStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string RoomKey(Guid roomId) => $"room:{roomId:D}:meta";
    private static string RoomsIndexKey => "rooms:index";
    private static string StateKey(Guid roomId) => $"room:{roomId:D}:state";

    private async Task<IDatabase> DbAsync()
    {
        var mux = await muxFactory.GetAsync().ConfigureAwait(false);
        return mux.GetDatabase();
    }

    public async Task SaveRoomAsync(RoomDto room, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = JsonSerializer.Serialize(room, JsonOpts);
        await db.StringSetAsync(RoomKey(room.RoomId), json).ConfigureAwait(false);
        await db.SortedSetAddAsync(RoomsIndexKey, room.RoomId.ToString("D"), room.CreatedAtUtc.ToUnixTimeSeconds())
                .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var ids = await db.SortedSetRangeByRankAsync(RoomsIndexKey, 0, -1, Order.Ascending)
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
        var db = await DbAsync().ConfigureAwait(false);

        var json = await db.StringGetAsync(RoomKey(roomId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<RoomDto>(json!, JsonOpts);
    }

    public async Task<RoomState?> GetStateAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = await db.StringGetAsync(StateKey(roomId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;

        var state = JsonSerializer.Deserialize<RoomState>(json!, JsonOpts);
        if (state is null) return null;

        // ✅ Fix: convert GameState back into the correct concrete type
        if (state.ActiveGame == GameType.Contexto && state.GameState is JsonElement je)
        {
            var cs = je.Deserialize<ContextoState>(JsonOpts);
            if (cs is not null)
                state = state with { GameState = cs };
        }

        return state;
    }

    public async Task SaveStateAsync(RoomState state, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = JsonSerializer.Serialize(state, JsonOpts);
        await db.StringSetAsync(StateKey(state.RoomId), json).ConfigureAwait(false);
    }

    public async Task<bool> DeleteRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        // Remove from index + delete keys
        var removed = await db.SortedSetRemoveAsync(RoomsIndexKey, roomId.ToString("D")).ConfigureAwait(false);
        await db.KeyDeleteAsync(
        [
        RoomKey(roomId),
        StateKey(roomId)
        ]).ConfigureAwait(false);

        return removed;
    }

}