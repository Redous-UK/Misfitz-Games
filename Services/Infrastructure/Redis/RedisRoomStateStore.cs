using Misfitz_Games.Data;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Room;
using StackExchange.Redis;
using System.Text.Json;

namespace Misfitz_Games.Services.Infrastructure.Redis;

public sealed class RedisRoomStateStore(AppDbContext db, RedisMuxFactory muxFactory) : IRoomStateStore
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
    private static string RoomStatsKey(Guid roomId) => $"room:{roomId:D}:stats";
    private static string LeaderboardKey(Guid roomId) => $"room:{roomId:D}:leaderboard";
    private static string LeaderboardGameKey(Guid roomId, GameType gameType) => $"room:{roomId:D}:leaderboard:{gameType.ToString().ToLowerInvariant()}";
    private static string LeaderboardUserKey(Guid roomId, string userId) => $"room:{roomId:D}:leaderboard:user:{userId}";
    private static string LeaderboardPersistedRoundKey(Guid roomId, string roundKey) => $"room:{roomId:D}:leaderboard:round:{roundKey}";

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
        var redis = await DbAsync().ConfigureAwait(false);

        var json = JsonSerializer.Serialize(room, JsonOpts);

        await redis.StringSetAsync(RoomKey(room.RoomId), json).ConfigureAwait(false);

        await redis.SortedSetAddAsync(
            RoomsIndexKey,
            room.RoomId.ToString("D"),
            room.CreatedAtUtc.ToUnixTimeSeconds()
        ).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(room.RoomCode))
        {
            await redis.StringSetAsync(
                RoomCodeKey(room.RoomCode),
                room.RoomId.ToString("D")
            ).ConfigureAwait(false);
        }
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

        var prevState = await GetStateAsync(state.RoomId, ct).ConfigureAwait(false);

        var leaderboardUpdate = LeaderboardUpdateFactory.TryCreate(prevState, state);
        if (leaderboardUpdate is not null)
            await AddToLeaderboardAsync(leaderboardUpdate, ct).ConfigureAwait(false);

        var json = JsonSerializer.Serialize(state, JsonOpts);
        await db.StringSetAsync(StateKey(state.RoomId), json).ConfigureAwait(false);
    }


    // ----------------------------
    // Room code mapping / resolving
    // ----------------------------
    public async Task<Guid?> ResolveRoomIdAsync(string roomRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roomRef))
            return null;

        roomRef = roomRef.Trim();

        // 1. Try GUID
        if (Guid.TryParse(roomRef, out var guid))
        {
            var exists = await db.Rooms
                .AnyAsync(r => r.Id == guid, ct);

            if (exists)
                return guid;
        }

        // 2. Try Code (THIS IS THE MISSING PIECE)
        var room = await db.Rooms
            .Where(r => r.Code == roomRef)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (room != Guid.Empty)
            return room;

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
    public async Task AddToLeaderboardAsync(LeaderboardUpdate update, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        if (update.ScoresByUserId is null || update.ScoresByUserId.Count == 0)
            return;

        var dedupeKey = LeaderboardPersistedRoundKey(update.RoomId, update.RoundKey);
        var firstWrite = await db.StringSetAsync(
            dedupeKey,
            "1",
            expiry: TimeSpan.FromDays(30),
            when: When.NotExists
        ).ConfigureAwait(false);

        if (!firstWrite)
            return;

        foreach (var kv in update.ScoresByUserId)
        {
            var userId = (kv.Key ?? "").Trim();
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            await db.SortedSetIncrementAsync(
                LeaderboardKey(update.RoomId),
                userId,
                kv.Value
            ).ConfigureAwait(false);

            await db.SortedSetIncrementAsync(
                LeaderboardGameKey(update.RoomId, update.GameType),
                userId,
                kv.Value
            ).ConfigureAwait(false);

            var username = update.UsernamesByUserId.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : userId;

            await db.HashSetAsync(
                LeaderboardUserKey(update.RoomId, userId),
                new[]
                {
                new HashEntry("username", username),
                new HashEntry("updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"))
                }
            ).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(update.WinnerUserId))
        {
            await db.HashIncrementAsync(
                LeaderboardUserKey(update.RoomId, update.WinnerUserId),
                "wins",
                1
            ).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        Guid roomId,
        int top = 20,
        CancellationToken ct = default)
    {
        return await GetLeaderboardInternalAsync(
            LeaderboardKey(roomId),
            roomId,
            top
        ).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        Guid roomId,
        GameType gameType,
        int top = 20,
        CancellationToken ct = default)
    {
        return await GetLeaderboardInternalAsync(
            LeaderboardGameKey(roomId, gameType),
            roomId,
            top
        ).ConfigureAwait(false);
    }

    public async Task<LeaderboardPlayerStatsDto?> GetLeaderboardPlayerAsync(
        Guid roomId,
        string userId,
        CancellationToken ct = default)
    {
        userId = (userId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var db = await DbAsync().ConfigureAwait(false);

        var entries = await db.HashGetAllAsync(LeaderboardUserKey(roomId, userId)).ConfigureAwait(false);
        if (entries.Length == 0)
            return null;

        string username = userId;
        int wins = 0;
        DateTimeOffset? updatedAtUtc = null;

        foreach (var e in entries)
        {
            var name = e.Name.ToString();
            var value = e.Value.ToString();

            if (name == "username" && !string.IsNullOrWhiteSpace(value))
                username = value;
            else if (name == "wins" && int.TryParse(value, out var parsedWins))
                wins = parsedWins;
            else if (name == "updatedAtUtc" && DateTimeOffset.TryParse(value, out var parsedDt))
                updatedAtUtc = parsedDt;
        }

        return new LeaderboardPlayerStatsDto(
            UserId: userId,
            Username: username,
            Wins: wins,
            UpdatedAtUtc: updatedAtUtc
        );
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardInternalAsync(
        string sortedSetKey,
        Guid roomId,
        int top)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var entries = await db.SortedSetRangeByRankWithScoresAsync(
            sortedSetKey,
            start: 0,
            stop: Math.Max(0, top - 1),
            order: Order.Descending
        ).ConfigureAwait(false);

        var results = new List<LeaderboardEntryDto>(entries.Length);

        foreach (var entry in entries)
        {
            var userId = entry.Element.ToString();
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            var userHash = await db.HashGetAllAsync(LeaderboardUserKey(roomId, userId)).ConfigureAwait(false);

            string username = userId;
            int wins = 0;
            DateTimeOffset? updatedAtUtc = null;

            foreach (var h in userHash)
            {
                var name = h.Name.ToString();
                var value = h.Value.ToString();

                if (name == "username" && !string.IsNullOrWhiteSpace(value))
                    username = value;
                else if (name == "wins" && int.TryParse(value, out var parsedWins))
                    wins = parsedWins;
                else if (name == "updatedAtUtc" && DateTimeOffset.TryParse(value, out var parsedDt))
                    updatedAtUtc = parsedDt;
            }

            results.Add(new LeaderboardEntryDto(
                UserId: userId,
                Username: username,
                Score: entry.Score,
                Wins: wins,
                UpdatedAtUtc: updatedAtUtc
            ));
        }

        return results;
    }

    public async Task IncrementGamesPlayedAsync(Guid roomId, long delta = 1, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        await db.HashIncrementAsync(RoomStatsKey(roomId), "gamesPlayed", delta).ConfigureAwait(false);
        await db.HashSetAsync(RoomStatsKey(roomId), "lastActivityUtc", DateTimeOffset.UtcNow.ToString("O")).ConfigureAwait(false);
    }

    public async Task IncrementGuessesTotalAsync(Guid roomId, long delta = 1, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        await db.HashIncrementAsync(RoomStatsKey(roomId), "guessesTotal", delta).ConfigureAwait(false);
        await db.HashSetAsync(RoomStatsKey(roomId), "lastActivityUtc", DateTimeOffset.UtcNow.ToString("O")).ConfigureAwait(false);
    }

    public async Task<RoomStatsDto> GetRoomStatsAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);

        var entries = await db.HashGetAllAsync(RoomStatsKey(roomId)).ConfigureAwait(false);

        long gamesPlayed = 0;
        long guessesTotal = 0;
        DateTimeOffset? last = null;

        foreach (var e in entries)
        {
            var name = e.Name.ToString();
            var val = e.Value.ToString();

            if (name == "gamesPlayed" && long.TryParse(val, out var gp)) gamesPlayed = gp;
            else if (name == "guessesTotal" && long.TryParse(val, out var gt)) guessesTotal = gt;
            else if (name == "lastActivityUtc" && DateTimeOffset.TryParse(val, out var dt)) last = dt;
        }

        return new RoomStatsDto(roomId, gamesPlayed, guessesTotal, last);
    }

    public async Task ResetRoomStatsAsync(Guid roomId, CancellationToken ct = default)
    {
        var db = await DbAsync().ConfigureAwait(false);
        await db.KeyDeleteAsync(RoomStatsKey(roomId)).ConfigureAwait(false);
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
            StateKey(roomId),
            LeaderboardKey(roomId),
            LeaderboardGameKey(roomId, GameType.Contexto),
            LeaderboardGameKey(roomId, GameType.Deal),
            LeaderboardGameKey(roomId, GameType.Hangman),
            LeaderboardGameKey(roomId, GameType.Trivia),
            LeaderboardGameKey(roomId, GameType.HigherLower),
            LeaderboardGameKey(roomId, GameType.RiddleMeThis),
            RoomStatsKey(roomId)
        ]).ConfigureAwait(false);

        // Release code mapping
        if (room is not null && !string.IsNullOrWhiteSpace(room.RoomCode))
            await db.KeyDeleteAsync(RoomCodeKey(room.RoomCode)).ConfigureAwait(false);
        await db.KeyDeleteAsync(RoomStatsKey(roomId)).ConfigureAwait(false);

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