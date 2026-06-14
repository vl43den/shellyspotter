using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;
using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "Agent,Employee,Admin")]
public class AgentController(AppDbContext db, AlertService alertService) : ControllerBase
{
    /// <summary>
    /// Full agent report: sensor reading + ping results in one call.
    /// </summary>
    [HttpPost("report")]
    public async Task<IActionResult> Report(AgentReportRequest request)
    {
        var room = await db.Rooms.FindAsync(request.RoomId);
        if (room is null) return NotFound();

        var reading = new SensorReading
        {
            RoomId = request.RoomId,
            Timestamp = DateTime.UtcNow,
            Temperature = request.Temperature,
            DoorOpen = request.DoorOpen,
            Brightness = request.Brightness,
            BatteryPercent = request.BatteryPercent
        };
        db.SensorReadings.Add(reading);

        if (request.PingResults.Count > 0)
        {
            var targetIds = request.PingResults.Select(r => r.PingTargetId).ToHashSet();
            var validIds = db.PingTargets
                .Where(t => t.RoomId == request.RoomId && targetIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToHashSet();

            db.PingResults.AddRange(request.PingResults
                .Where(r => validIds.Contains(r.PingTargetId))
                .Select(r => new PingResult
                {
                    PingTargetId = r.PingTargetId,
                    Timestamp = DateTime.UtcNow,
                    IsReachable = r.IsReachable,
                    RoundTripMs = r.RoundTripMs
                }));
        }

        await db.SaveChangesAsync();
        await alertService.HandleSensorReadingAsync(reading);

        if (!reading.DoorOpen)
            await alertService.ResolveDoorAlertsAsync(request.RoomId);

        return Ok();
    }

    /// <summary>
    /// Returns the ping targets the agent should check for a given room.
    /// </summary>
    [HttpGet("rooms/{roomId:int}/targets")]
    public async Task<ActionResult<IEnumerable<PingTargetDto>>> GetTargets(int roomId)
    {
        var targets = await db.PingTargets
            .Where(t => t.RoomId == roomId && t.IsEnabled)
            .Select(t => new PingTargetDto(t.Id, t.RoomId, t.Name, t.IpAddress, t.IsEnabled))
            .ToListAsync();
        return Ok(targets);
    }
}
