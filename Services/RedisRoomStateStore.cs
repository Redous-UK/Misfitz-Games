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
    private static string StateKey(Guid roomId) => $"room:{roomId:D}:state";
    private static string RoomsIndexKey => "rooms:index";
    private static string RoomCodeKey(string code) => $"roomcode:{NormalizeCode(code)}";
    private static string LeaderboardKey(Guid roomId) => $"room:{roomId:D}:leaderboard";

    private static string NormalizeCode(string code)
        => (code ?? "").Trim().ToUpperInvariant();

    private async Task<IDatabase> DbAsync()
    {
        var mux = await muxFactory.GetAsync().ConfigureAwait(false);
        return mux.GetDatabase();
    }

    // ----------------------------
    // Room meta
    // ----------------------------
    public async Task SaveRoomAsync(RoomDto room, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = JsonSerializer.Serialize(room, JsonOpts);

        await db.StringSetAsync(RoomKey(room.RoomId), json).ConfigureAwait(false);

        // Index by created time (score)
        await db.SortedSetAddAsync(
            RoomsIndexKey,
            room.RoomId.ToString("D"),
            room.CreatedAtUtc.ToUnixTimeSeconds()
        ).ConfigureAwait(false);

        // NOTE: we do NOT write roomcode mapping here because codes are reserved atomically
        // via TryReserveRoomCodeAsync(...) during room creation.
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

    // ----------------------------
    // Room state
    // ----------------------------
    public async Task<RoomState?> GetStateAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var json = await db.StringGetAsync(StateKey(roomId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty) return null;

        var state = JsonSerializer.Deserialize<RoomState>(json!, JsonOpts);
        if (state is null) return null;

        // If RoomState.GameState is object?, System.Text.Json will often round-trip as JsonElement.
        // Normalize Contexto state so the engine can operate on concrete ContextoState.
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

    // ----------------------------
    // Room code mapping / resolving
    // ----------------------------
    public async Task<Guid?> ResolveRoomIdAsync(string roomRef, CancellationToken ct = default)
    {
        roomRef = (roomRef ?? "").Trim();

        // GUID path
        if (Guid.TryParse(roomRef, out var guid))
            return guid;

        // Code path (numeric 8-digit or custom)
        var code = NormalizeCode(roomRef);
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var db = await DbAsync().ConfigureAwait(false);
        var val = await db.StringGetAsync(RoomCodeKey(code)).ConfigureAwait(false);

        if (!val.IsNullOrEmpty && Guid.TryParse(val.ToString(), out var resolved))
            return resolved;

        return null;
    }

    public async Task<bool> TryReserveRoomCodeAsync(string roomCode, Guid roomId, CancellationToken ct = default)
    {
        var code = NormalizeCode(roomCode);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var db = await DbAsync().ConfigureAwait(false);

        // Atomic: only reserve if not exists => no collisions ever.
        return await db.StringSetAsync(
            RoomCodeKey(code),
            roomId.ToString("D"),
            expiry: null,
            when: When.NotExists
        ).ConfigureAwait(false);
    }

    public async Task ReleaseRoomCodeAsync(string roomCode, CancellationToken ct = default)
    {
        var code = NormalizeCode(roomCode);
        if (string.IsNullOrWhiteSpace(code))
            return;

        var db = await DbAsync().ConfigureAwait(false);
        await db.KeyDeleteAsync(RoomCodeKey(code)).ConfigureAwait(false);
    }

    // ----------------------------
    // Leaderboard
    // ----------------------------
    public async Task AddToLeaderboardAsync(Guid roomId, IReadOnlyDictionary<string, int> deltaByUserId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        foreach (var kv in deltaByUserId)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            await db.SortedSetIncrementAsync(LeaderboardKey(roomId), kv.Key, kv.Value).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<(string userId, double score)>> GetLeaderboardAsync(Guid roomId, int top = 20, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var entries = await db.SortedSetRangeByRankWithScoresAsync(
            LeaderboardKey(roomId),
            start: 0,
            stop: top - 1,
            order: Order.Descending
        ).ConfigureAwait(false);

        return [.. entries.Select(e => (userId: e.Element.ToString(), score: e.Score))];
    }

    // ----------------------------
    // Delete room
    // ----------------------------
    public async Task<bool> DeleteRoomAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        // Load room to get the code (so we can release mapping)
        var room = await GetRoomAsync(roomId, ct).ConfigureAwait(false);

        // Remove from index
        var removedFromIndex = await db
            .SortedSetRemoveAsync(RoomsIndexKey, roomId.ToString("D"))
            .ConfigureAwait(false);

        // Delete state + meta
        await db.KeyDeleteAsync(
        [
            RoomKey(roomId),
            StateKey(roomId)
        ]).ConfigureAwait(false);

        // Release code mapping
        if (room is not null && !string.IsNullOrWhiteSpace(room.RoomCode))
            await db.KeyDeleteAsync(RoomCodeKey(room.RoomCode)).ConfigureAwait(false);

        return removedFromIndex;
    }

    // ----------------------------
    // Cleanup preview + delete older rooms
    // ----------------------------
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

    public async Task<int> DeleteRoomsOlderThanAsync(DateTimeOffset cutoffUtc, int maxToDelete = 200, CancellationToken ct = default)
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
            take: maxToDelete
        ).ConfigureAwait(false);

        if (ids.Length == 0) return 0;

        var deleted = 0;

        foreach (var idVal in ids)
        {
            if (!Guid.TryParse(idVal.ToString(), out var id)) continue;

            // Use the canonical delete so meta/state/index/code are all cleaned.
            await DeleteRoomAsync(id, ct).ConfigureAwait(false);
            deleted++;
        }

        return deleted;
    }
}