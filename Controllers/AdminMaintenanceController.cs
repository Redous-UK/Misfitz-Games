using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Misfitz_Games.Services;

namespace Misfitz_Games.Controllers;

[ApiController]
public class AdminMaintenanceController(IRoomStateStore store) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/admin/rooms/cleanup")]
    public async Task<IActionResult> CleanupRooms([FromQuery] int olderThanHours = 24, [FromQuery] int max = 200, CancellationToken ct = default)
    {
        if (olderThanHours < 1) olderThanHours = 1;
        if (max < 1) max = 1;
        if (max > 2000) max = 2000; // safety cap

        var cutoffUtc = DateTimeOffset.UtcNow.AddHours(-olderThanHours);
        var deleted = await store.DeleteRoomsOlderThanAsync(cutoffUtc, max, ct);

        return Ok(new
        {
            ok = true,
            olderThanHours,
            max,
            cutoffUtc,
            deleted
        });
    }
}