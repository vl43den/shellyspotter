using System.Security.Claims;
using ShellySpotter.Core.Data;

namespace ShellySpotter.Core.Services;

/// <summary>
/// Central authorization check for room-scoped resources.
/// Employees, Admins and the Agent may access any room; a Customer may only
/// access rooms they own. Used by every controller that exposes data under
/// <c>/api/rooms/{roomId}/...</c> to prevent one customer reading another's data.
/// </summary>
public class RoomAccessService(AppDbContext db)
{
    public async Task<bool> CanAccessRoomAsync(ClaimsPrincipal user, int roomId)
    {
        var role = user.FindFirst("role")?.Value;
        if (role is "Employee" or "Admin" or "Agent")
            return true;

        var userId = user.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return false;

        var room = await db.Rooms.FindAsync(roomId);
        return room is not null && room.OwnerId == userId;
    }
}
