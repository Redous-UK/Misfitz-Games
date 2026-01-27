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

    private static string RoomCodeKey(string code) => $"roomcode:{code}";

    private async Task<IDatabase> DbAsync()
    {
        var mux = await muxFactory.GetAsync().ConfigureAwait(false);
        return mux.GetDatabase();
    }

    public async Task SaveRoomAsync(RoomDto room, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = JsonSerializer.Serialize(room, JsonOpts);
        await db.StringSetAsync(RoomCodeKey(room.RoomCode), room.RoomId.ToString("D")).ConfigureAwait(false);
        await db.SortedSetAddAsync(RoomsIndexKey, room.RoomId.ToString("D"), room.CreatedAtUtc.ToUnixTimeSeconds())
                .ConfigureAwait(false);
    }

    public async Task<Guid?> ResolveRoomIdAsync(string roomRef, CancellationToken ct = default)
    {
        if (Guid.TryParse(roomRef, out var guid))
            return guid;

        if (roomRef.Length == 8 && roomRef.All(char.IsDigit))
        {
            var db = await DbAsync().ConfigureAwait(false);
            var val = await db.StringGetAsync(RoomCodeKey(roomRef)).ConfigureAwait(false);
            if (!val.IsNullOrEmpty && Guid.TryParse(val.ToString(), out var resolved))
                return resolved;
        }

        return null;
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

        var room = await GetRoomAsync(roomId, ct).ConfigureAwait(false);
        if (room is not null)
            await db.KeyDeleteAsync(RoomCodeKey(room.RoomCode)).ConfigureAwait(false);

        // Remove from index + delete keys
        var removed = await db.SortedSetRemoveAsync(RoomsIndexKey, roomId.ToString("D")).ConfigureAwait(false);
        await db.KeyDeleteAsync(
        [
        RoomKey(roomId),
        StateKey(roomId)
        ]).ConfigureAwait(false);

        return removed;
    }

    public async Task<int> DeleteRoomsOlderThanAsync(DateTimeOffset cutoffUtc, int maxToDelete = 200, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var cutoffScore = cutoffUtc.ToUnixTimeSeconds();

        // Get room IDs older than cutoff (by score)
        var ids = await db.SortedSetRangeByScoreAsync(
            RoomsIndexKey,
            start: double.NegativeInfinity,
            stop: cutoffScore,
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: maxToDelete
        ).ConfigureAwait(false);

        if (ids.Length == 0) return 0;

        // Build delete batch
        var keysToDelete = new List<RedisKey>(ids.Length * 2);
        foreach (var idVal in ids)
        {
            if (!Guid.TryParse(idVal.ToString(), out var id)) continue;

            // remove from index
            await db.SortedSetRemoveAsync(RoomsIndexKey, id.ToString("D")).ConfigureAwait(false);

            // delete meta/state
            keysToDelete.Add(RoomKey(id));
            keysToDelete.Add(StateKey(id));
        }

        if (keysToDelete.Count > 0)
            await db.KeyDeleteAsync([.. keysToDelete]).ConfigureAwait(false);

        return ids.Length;
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsOlderThanAsync(DateTimeOffset cutoffUtc, int max = 200, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var cutoffScore = cutoffUtc.ToUnixTimeSeconds();

        var ids = await db.SortedSetRangeByScoreAsync(
            RoomsIndexKey,
            start: double.NegativeInfinity,
            stop: cutoffScore,
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: max
        ).ConfigureAwait(false);

        if (ids.Length == 0) return [];

        var results = new List<RoomDto>(ids.Length);
        foreach (var idVal in ids)
        {
            if (!Guid.TryParse(idVal.ToString(), out var id)) continue;
            var room = await GetRoomAsync(id, ct).ConfigureAwait(false);
            if (room is not null) results.Add(room);
        }

        return results;
    }

    public static class RoomCodeGenerator
    {
        private static readonly Random _rng = new();

        public static string NewCode()
        {
            // 8 digits, leading zeros allowed
            return _rng.Next(0, 100_000_000).ToString("D8");
        }
    }

}