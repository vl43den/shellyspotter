using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;

namespace ShellySpotter.Core.Services;

public class MaintenanceWindowService(AppDbContext db)
{
    public async Task<bool> IsWithinMaintenanceWindowAsync(int roomId, DateTime utcNow)
    {
        var windows = await db.MaintenanceWindows
            .Where(w => w.RoomId == roomId && w.DayOfWeek == utcNow.DayOfWeek)
            .ToListAsync();

        var timeOfDay = utcNow.TimeOfDay;
        return windows.Any(w => timeOfDay >= w.StartTime && timeOfDay <= w.EndTime);
    }
}
