using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/rooms/{roomId:int}/alerts")]
[Authorize]
public class AlertsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertDto>>> GetAlerts(int roomId, [FromQuery] bool openOnly = false)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();

        IQueryable<Models.Alert> query = db.Alerts.Where(a => a.RoomId == roomId);
        if (openOnly) query = query.Where(a => a.ResolvedAt == null);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertDto(a.Id, a.RoomId, a.Type, a.Message, a.CreatedAt, a.ResolvedAt, a.TicketUrl))
            .ToListAsync();

        return Ok(alerts);
    }

    [HttpPost("{id:int}/resolve")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> ResolveAlert(int roomId, int id)
    {
        var alert = await db.Alerts.FindAsync(id);
        if (alert is null || alert.RoomId != roomId) return NotFound();

        alert.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
