using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;
using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/rooms/{roomId:int}/readings")]
[Authorize]
public class SensorReadingsController(AppDbContext db, AlertService alertService, RoomAccessService roomAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SensorReadingDto>>> GetReadings(int roomId, [FromQuery] int limit = 100)
    {
        if (!await roomAccess.CanAccessRoomAsync(User, roomId)) return Forbid();

        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();

        var readings = await db.SensorReadings
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .Select(r => new SensorReadingDto(r.Id, r.RoomId, r.Timestamp, r.Temperature, r.DoorOpen, r.Brightness, r.BatteryPercent))
            .ToListAsync();

        return Ok(readings);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<SensorReadingDto>> GetLatest(int roomId)
    {
        if (!await roomAccess.CanAccessRoomAsync(User, roomId)) return Forbid();

        var reading = await db.SensorReadings
            .Where(r => r.RoomId == roomId)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync();

        if (reading is null) return NotFound();

        return Ok(new SensorReadingDto(reading.Id, reading.RoomId, reading.Timestamp, reading.Temperature, reading.DoorOpen, reading.Brightness, reading.BatteryPercent));
    }

    [HttpPost]
    [Authorize(Roles = "Agent,Employee,Admin")]
    public async Task<ActionResult<SensorReadingDto>> CreateReading(int roomId, CreateSensorReadingRequest request)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();

        var reading = new SensorReading
        {
            RoomId = roomId,
            Timestamp = DateTime.UtcNow,
            Temperature = request.Temperature,
            DoorOpen = request.DoorOpen,
            Brightness = request.Brightness,
            BatteryPercent = request.BatteryPercent
        };

        db.SensorReadings.Add(reading);
        await db.SaveChangesAsync();
        await alertService.HandleSensorReadingAsync(reading);

        if (!reading.DoorOpen)
            await alertService.ResolveDoorAlertsAsync(roomId);

        return Ok(new SensorReadingDto(reading.Id, reading.RoomId, reading.Timestamp, reading.Temperature, reading.DoorOpen, reading.Brightness, reading.BatteryPercent));
    }
}
