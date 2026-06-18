using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;
using Xunit;

namespace ShellySpotter.Core.Tests;

public class MaintenanceWindowServiceTests
{
    private const int RoomId = 1001;

    // 2024-01-01 is a Monday, 10:00 UTC.
    private static readonly DateTime MondayMorning = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static async Task<MaintenanceWindowService> WithWindowAsync(DayOfWeek day, TimeSpan start, TimeSpan end)
    {
        var db = TestSupport.NewDb();
        db.MaintenanceWindows.Add(new MaintenanceWindow { RoomId = RoomId, DayOfWeek = day, StartTime = start, EndTime = end });
        await db.SaveChangesAsync();
        return new MaintenanceWindowService(db);
    }

    [Fact]
    public async Task True_when_time_inside_window()
    {
        var svc = await WithWindowAsync(DayOfWeek.Monday, new(9, 0, 0), new(17, 0, 0));
        Assert.True(await svc.IsWithinMaintenanceWindowAsync(RoomId, MondayMorning));
    }

    [Fact]
    public async Task False_when_time_before_window_starts()
    {
        var svc = await WithWindowAsync(DayOfWeek.Monday, new(11, 0, 0), new(17, 0, 0));
        Assert.False(await svc.IsWithinMaintenanceWindowAsync(RoomId, MondayMorning)); // 10:00 < 11:00
    }

    [Fact]
    public async Task False_on_a_different_weekday()
    {
        var svc = await WithWindowAsync(DayOfWeek.Tuesday, new(9, 0, 0), new(17, 0, 0));
        Assert.False(await svc.IsWithinMaintenanceWindowAsync(RoomId, MondayMorning));
    }

    [Fact]
    public async Task False_when_no_windows_configured()
    {
        var svc = new MaintenanceWindowService(TestSupport.NewDb());
        Assert.False(await svc.IsWithinMaintenanceWindowAsync(RoomId, MondayMorning));
    }
}
