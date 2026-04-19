using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Games;
using Misfitz_Games.Services.Room;
using StackExchange.Redis;
using System.Text.Json;
using System.Xml;

namespace Misfitz_Games.Services.Infrastructure.Redis;

public sealed class RedisRoomStateStore(IServiceScopeFactory scopeFactory, RedisMuxFactory muxFactory) : IRoomStateStore
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

    private async Task<AppDbContext> CreateAppDbContextAsync()
    {
        var scope = scopeFactory.CreateScope();
        try
        {
            return scope.ServiceProvider.GetRequiredService<AppDbContext>();
        }
        catch
        {
            scope.Dispose();
            throw;
        }
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

        if (state.GameState is JsonElement je)
        {
            object? typedState = state.ActiveGame switch
            {
                GameType.Contexto => je.Deserialize<ContextoState>(JsonOpts),
                GameType.Hangman => je.Deserialize<HangmanState>(JsonOpts),
                //GameType.Trivia => je.Deserialize<TriviaState>(JsonOpts),
                GameType.HigherLower => je.Deserialize<HigherLowerState>(JsonOpts),
                GameType.RiddleMeThis => je.Deserialize<RiddleMeThisState>(JsonOpts),
                //GameType.Deal => je.Deserialize<DealState>(JsonOpts),
                _ => null
            };

            if (typedState is not null)
                state = state with { GameState = typedState };
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

        var redis = await DbAsync().ConfigureAwait(false);

        // 1. Try Redis room-code mapping first
        var mapped = await redis.StringGetAsync(RoomCodeKey(roomRef)).ConfigureAwait(false);
        if (!mapped.IsNullOrEmpty && Guid.TryParse(mapped.ToString(), out var mappedRoomId))
        {
            using var scope = scopeFactory.CreateScope();
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var isActive = await appDb.Rooms
                .AsNoTracking()
                .AnyAsync(r => r.Id == mappedRoomId && r.IsActive, ct)
                .ConfigureAwait(false);

            if (isActive)
                return mappedRoomId;

            // stale redis mapping for inactive room
            await redis.KeyDeleteAsync(RoomCodeKey(roomRef)).ConfigureAwait(false);
            return null;
        }

        // 2. Try direct room Guid
        if (Guid.TryParse(roomRef, out var guid))
        {
            using var scope = scopeFactory.CreateScope();
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var exists = await appDb.Rooms
                .AsNoTracking()
                .AnyAsync(r => r.Id == guid && r.IsActive, ct)
                .ConfigureAwait(false);

            if (exists)
                return guid;
        }

        // 3. Try SQL by code, active only, then rehydrate Redis mapping
        using (var scope = scopeFactory.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var roomId = await appDb.Rooms
                .AsNoTracking()
                .Where(r => r.Code == roomRef && r.IsActive)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (roomId is not null)
            {
                await redis.StringSetAsync(
                    RoomCodeKey(roomRef),
                    roomId.Value.ToString("D")
                ).ConfigureAwait(false);

                return roomId.Value;
            }
        }

        return null;
    }

    public async Task<bool> TryReserveRoomCodeAsync(string roomCode, Guid roomId, CancellationToken ct = default)
    {
        var code = NormalizeCode(roomCode);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var db = await DbAsync().ConfigureAwait(false);

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
                [
                    new HashEntry("username", username),
                    new HashEntry("updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"))
                ]
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

        var room = await GetRoomAsync(roomId, ct).ConfigureAwait(false);

        var removedFromIndex = await db
            .SortedSetRemoveAsync(RoomsIndexKey, roomId.ToString("D"))
            .ConfigureAwait(false);

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
            await DeleteRoomAsync(id, ct).ConfigureAwait(false);
            deleted++;
        }

        return deleted;
    }

    public async Task<bool> MarkRoomInactiveAsync(Guid roomId, CancellationToken ct = default)
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var roomEntity = await appDb.Rooms
                .FirstOrDefaultAsync(r => r.Id == roomId, ct)
                .ConfigureAwait(false);

            if (roomEntity is null)
                return false;

            roomEntity.IsActive = false;
            await appDb.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var redis = await DbAsync().ConfigureAwait(false);

        var room = await GetRoomAsync(roomId, ct).ConfigureAwait(false);

        await redis.SortedSetRemoveAsync(RoomsIndexKey, roomId.ToString("D")).ConfigureAwait(false);

        await redis.KeyDeleteAsync(
        [
        RoomKey(roomId),
        StateKey(roomId),
        RoomStatsKey(roomId)
        ]).ConfigureAwait(false);

        if (room is not null && !string.IsNullOrWhiteSpace(room.RoomCode))
            await redis.KeyDeleteAsync(RoomCodeKey(room.RoomCode)).ConfigureAwait(false);

        return true;
    }
}
