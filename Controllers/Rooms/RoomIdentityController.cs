using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Data;
using Misfitz_Games.Models;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Misfitz_Games.Controllers.Rooms;

[ApiController]
public sealed class RoomIdentityController(AppDbContext db) : ControllerBase
{
    [HttpGet("/member/room")]
    [Authorize(Policy = "MemberOrAdmin")]
    public async Task<IActionResult> MyRoom()
    {

        static string? GetUserIdClaim(ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier)
         ?? user.FindFirstValue("userId")
         ?? user.FindFirstValue("userid")
         ?? user.FindFirstValue("uid")
         ?? user.FindFirstValue("id")
         ?? user.FindFirstValue("sub");

        var userIdStr = GetUserIdClaim(User);
        if (!long.TryParse(userIdStr, out var userId))
            return Unauthorized(new { ok = false, error = "Missing user id claim." });

        var room = await db.Rooms.SingleOrDefaultAsync(r => r.OwnerUserId == userId);
        if (room is not null)
            return Ok(new { ok = true, roomId = room.Id, roomCode = room.Code, name = room.Name });

        var code = await GenerateUniqueCode(db);

        room = new Room(
            Guid.NewGuid(),
            code,
            userId,
            "My Room",
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        return Ok(new { ok = true, roomId = room.Id, roomCode = room.Code, name = room.Name });
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