using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.DTOs;
using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;

namespace ShellySpotter.Core.Controllers;

[ApiController]
[Route("api/rooms/{roomId:int}/ping-targets")]
[Authorize]
public class PingTargetsController(AppDbContext db, RoomAccessService roomAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PingTargetDto>>> GetTargets(int roomId)
    {
        if (!await roomAccess.CanAccessRoomAsync(User, roomId)) return Forbid();

        var targets = await db.PingTargets
            .Where(t => t.RoomId == roomId)
            .Select(t => new PingTargetDto(t.Id, t.RoomId, t.Name, t.IpAddress, t.IsEnabled))
            .ToListAsync();
        return Ok(targets);
    }

    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<ActionResult<PingTargetDto>> CreateTarget(int roomId, CreatePingTargetRequest request)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();

        var target = new PingTarget { RoomId = roomId, Name = request.Name, IpAddress = request.IpAddress };
        db.PingTargets.Add(target);
        await db.SaveChangesAsync();
        return Ok(new PingTargetDto(target.Id, target.RoomId, target.Name, target.IpAddress, target.IsEnabled));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> DeleteTarget(int roomId, int id)
    {
        var target = await db.PingTargets.FindAsync(id);
        if (target is null || target.RoomId != roomId) return NotFound();
        db.PingTargets.Remove(target);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/results")]
    public async Task<ActionResult<IEnumerable<PingResultDto>>> GetResults(int roomId, int id, [FromQuery] int limit = 50)
    {
        if (!await roomAccess.CanAccessRoomAsync(User, roomId)) return Forbid();

        // Ensure the target actually belongs to this room — otherwise an
        // authorized room owner could read results of an unrelated room's target.
        var targetInRoom = await db.PingTargets.AnyAsync(t => t.Id == id && t.RoomId == roomId);
        if (!targetInRoom) return NotFound();

        var results = await db.PingResults
            .Where(r => r.PingTargetId == id)
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .Select(r => new PingResultDto(r.Id, r.PingTargetId, r.Timestamp, r.IsReachable, r.RoundTripMs))
            .ToListAsync();
        return Ok(results);
    }

    [HttpPost("ping-results")]
    [Authorize(Roles = "Agent,Employee,Admin")]
    public async Task<IActionResult> SubmitPingResults(int roomId, SubmitPingResultsRequest request)
    {
        var targetIds = request.Results.Select(r => r.PingTargetId).ToHashSet();
        var validIds = (await db.PingTargets
            .Where(t => t.RoomId == roomId && targetIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync()).ToHashSet();

        var results = request.Results
            .Where(r => validIds.Contains(r.PingTargetId))
            .Select(r => new PingResult
            {
                PingTargetId = r.PingTargetId,
                Timestamp = DateTime.UtcNow,
                IsReachable = r.IsReachable,
                RoundTripMs = r.RoundTripMs
            });

        db.PingResults.AddRange(results);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
