using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;
using ShellySpotter.Core.Models;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoomsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetRooms()
    {
        var userRole = User.FindFirst("role")?.Value;
        var userId = User.FindFirst("sub")?.Value ?? string.Empty;

        IQueryable<Room> query = db.Rooms;
        if (userRole == "Customer")
            query = query.Where(r => r.OwnerId == userId);

        var rooms = await query
            .Select(r => new RoomDto(r.Id, r.Name, r.Description, r.OwnerId, r.HighTemperatureThreshold))
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> GetRoom(int id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        if (!CanAccessRoom(room)) return Forbid();

        return Ok(new RoomDto(room.Id, room.Name, room.Description, room.OwnerId, room.HighTemperatureThreshold));
    }

    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<RoomDto>> CreateRoom(CreateRoomRequest request)
    {
        var room = new Room
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = request.OwnerId,
            HighTemperatureThreshold = request.HighTemperatureThreshold
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var dto = new RoomDto(room.Id, room.Name, room.Description, room.OwnerId, room.HighTemperatureThreshold);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> UpdateRoom(int id, UpdateRoomRequest request)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();

        room.Name = request.Name;
        room.Description = request.Description;
        room.HighTemperatureThreshold = request.HighTemperatureThreshold;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();

        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private bool CanAccessRoom(Room room)
    {
        var role = User.FindFirst("role")?.Value;
        var userId = User.FindFirst("sub")?.Value;
        return role is "Employee" or "Admin" || room.OwnerId == userId;
    }
}
