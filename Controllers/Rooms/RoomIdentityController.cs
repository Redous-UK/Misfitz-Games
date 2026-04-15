using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using Misfitz_Games.Services.Room;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Misfitz_Games.Controllers.Rooms;

[ApiController]
public sealed class RoomIdentityController(AppDbContext db, IRoomStateStore store) : ControllerBase
{
    [HttpGet("/member/room")]
    [Authorize(Policy = "MemberOrAdmin")]
    public async Task<IActionResult> MyRoom()
    {
        await EnsureCoreTablesAsync(db);

        static string? GetUserIdClaim(ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("sub");

        var userGuidValue = GetUserIdClaim(User);
        if (string.IsNullOrWhiteSpace(userGuidValue))
            return Unauthorized(new { ok = false, error = "Missing user id claim." });

        var map = await db.UserIdMaps.SingleOrDefaultAsync(x => x.UserGuid == userGuidValue);
        if (map is null)
        {
            map = new UserIdMap { UserGuid = userGuidValue };
            db.UserIdMaps.Add(map);
            await db.SaveChangesAsync();
        }

        if (!Guid.TryParse(map.UserGuid, out var ownerUserId))
            return BadRequest(new { ok = false, error = "Invalid user guid in mapping." });

        var room = await db.Rooms.SingleOrDefaultAsync(r => r.OwnerUserId == ownerUserId);
        if (room is not null)
            return Ok(new { ok = true, roomId = room.Id, roomCode = room.Code, name = room.Name });

        var code = await GenerateUniqueCode(db);

        room = new Room
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Name = "My Room",
            Code = code,
            Description = null,
            CreatedUtc = DateTime.UtcNow,
            LastActiveUtc = DateTime.UtcNow,
            DefaultGame = "None",
            AutoRestore = true,
            AllowGuests = true,
            OverlaysEnabled = true,
            IsPrivate = false
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        await store.SaveRoomAsync(
            new RoomDto(
                RoomId: room.Id,
                Name: room.Name,
                CreatedAtUtc: new DateTimeOffset(room.CreatedUtc),
                RoomCode: room.Code
            )
        );

        return Ok(new { ok = true, roomId = room.Id, roomCode = room.Code, name = room.Name });
    }

    static async Task EnsureCoreTablesAsync(AppDbContext db)
    {
        if (!db.Database.IsSqlite()) return;

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"

-- UserIdMaps
CREATE TABLE IF NOT EXISTS UserIdMaps (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  UserGuid TEXT NOT NULL,
  CreatedUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_UserIdMaps_UserGuid
  ON UserIdMaps(UserGuid);

-- AppUser
CREATE TABLE IF NOT EXISTS AppUser (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Username TEXT NOT NULL
);

-- Rooms
CREATE TABLE IF NOT EXISTS Rooms (
  Id TEXT NOT NULL PRIMARY KEY,
  Code TEXT NOT NULL,
  OwnerUserId TEXT NOT NULL,
  Name TEXT NOT NULL,
  Description TEXT NULL,
  CreatedUtc TEXT NOT NULL,
  LastActiveUtc TEXT NOT NULL,
  DefaultGame TEXT NOT NULL DEFAULT 'None',
  AutoRestore INTEGER NOT NULL DEFAULT 1,
  AllowGuests INTEGER NOT NULL DEFAULT 1,
  OverlaysEnabled INTEGER NOT NULL DEFAULT 1,
  IsPrivate INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_Rooms_OwnerUserId_Code
  ON Rooms(OwnerUserId, Code);

-- TikTokLinks (if used)
CREATE TABLE IF NOT EXISTS TikTokLinks (
  Id TEXT NOT NULL PRIMARY KEY,
  UserId TEXT NOT NULL,
  TikTokOpenId TEXT NOT NULL,
  TikTokUsername TEXT,
  AccessTokenEnc TEXT NOT NULL,
  RefreshTokenEnc TEXT NOT NULL,
  AccessTokenExpiresUtc TEXT NOT NULL,
  Scopes TEXT NOT NULL,
  CreatedUtc TEXT NOT NULL,
  UpdatedUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_TikTokLinks_UserId
  ON TikTokLinks(UserId);

";

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string> GenerateUniqueCode(AppDbContext db)
    {
        for (int i = 0; i < 25; i++)
        {
            var code = NewRoomCode();
            if (!await db.Rooms.AnyAsync(r => r.Code == code))
                return code;
        }
        throw new Exception("Failed to allocate unique room code.");
    }

    private static string NewRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buf = stackalloc char[8];
        for (int i = 0; i < buf.Length; i++)
            buf[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        return new string(buf);
    }
}