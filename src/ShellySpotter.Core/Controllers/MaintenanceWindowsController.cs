using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;
using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/rooms/{roomId:int}/maintenance-windows")]
[Authorize]
public class MaintenanceWindowsController(AppDbContext db, RoomAccessService roomAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaintenanceWindowDto>>> GetWindows(int roomId)
    {
        if (!await roomAccess.CanAccessRoomAsync(User, roomId)) return Forbid();

        var windows = await db.MaintenanceWindows
            .Where(w => w.RoomId == roomId)
            .Select(w => new MaintenanceWindowDto(w.Id, w.RoomId, w.DayOfWeek, w.StartTime, w.EndTime, w.Label))
            .ToListAsync();
        return Ok(windows);
    }

    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<MaintenanceWindowDto>> CreateWindow(int roomId, CreateMaintenanceWindowRequest request)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();

        var window = new MaintenanceWindow
        {
            RoomId = roomId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Label = request.Label
        };
        db.MaintenanceWindows.Add(window);
        await db.SaveChangesAsync();
        return Ok(new MaintenanceWindowDto(window.Id, window.RoomId, window.DayOfWeek, window.StartTime, window.EndTime, window.Label));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> DeleteWindow(int roomId, int id)
    {
        var window = await db.MaintenanceWindows.FindAsync(id);
        if (window is null || window.RoomId != roomId) return NotFound();
        db.MaintenanceWindows.Remove(window);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
