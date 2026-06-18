using Microsoft.Extensions.Logging.Abstractions;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.Models;
using ShellySpotter.Core.Services;
using Xunit;

namespace ShellySpotter.Core.Tests;

public class AlertServiceTests
{
    private const int RoomId = 1001;

    private static AlertService Build(AppDbContext db, FakeTicketService? tickets = null) =>
        new(db, new MaintenanceWindowService(db), tickets ?? new FakeTicketService(), NullLogger<AlertService>.Instance);

    private static async Task<AppDbContext> DbWithRoomAsync(double threshold = 28.0)
    {
        var db = TestSupport.NewDb();
        db.Rooms.Add(new Room { Id = RoomId, Name = "Server Room", OwnerId = "alice", HighTemperatureThreshold = threshold });
        await db.SaveChangesAsync();
        return db;
    }

    private static SensorReading Reading(double? temp = null, bool door = false) => new()
    {
        RoomId = RoomId,
        Timestamp = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
        Temperature = temp,
        DoorOpen = door
    };

    [Fact]
    public async Task Temperature_above_threshold_raises_alert_and_ticket()
    {
        var db = await DbWithRoomAsync(threshold: 28);
        var tickets = new FakeTicketService();

        await Build(db, tickets).HandleSensorReadingAsync(Reading(temp: 35));

        var alert = Assert.Single(db.Alerts.Where(a => a.Type == "TemperatureHigh"));
        Assert.Null(alert.ResolvedAt);
        Assert.Equal("http://redmine.local/issues/1", alert.TicketUrl);
        Assert.Equal(1, tickets.CallCount);
    }

    [Fact]
    public async Task Temperature_below_threshold_raises_no_alert()
    {
        var db = await DbWithRoomAsync(threshold: 28);
        await Build(db).HandleSensorReadingAsync(Reading(temp: 22));
        Assert.Empty(db.Alerts);
    }

    [Fact]
    public async Task Second_high_reading_does_not_duplicate_the_alert()
    {
        var db = await DbWithRoomAsync(threshold: 28);
        var svc = Build(db);

        await svc.HandleSensorReadingAsync(Reading(temp: 35));
        await svc.HandleSensorReadingAsync(Reading(temp: 36));

        Assert.Single(db.Alerts.Where(a => a.Type == "TemperatureHigh" && a.ResolvedAt == null));
    }

    [Fact]
    public async Task Cooling_below_hysteresis_resolves_the_alert()
    {
        var db = await DbWithRoomAsync(threshold: 28);
        var svc = Build(db);

        await svc.HandleSensorReadingAsync(Reading(temp: 35)); // raise
        await svc.HandleSensorReadingAsync(Reading(temp: 26)); // <= 28 - 1 hysteresis → resolve

        Assert.Empty(db.Alerts.Where(a => a.Type == "TemperatureHigh" && a.ResolvedAt == null));
        Assert.Single(db.Alerts.Where(a => a.Type == "TemperatureHigh" && a.ResolvedAt != null));
    }

    [Fact]
    public async Task Door_open_outside_maintenance_raises_alert()
    {
        var db = await DbWithRoomAsync();
        await Build(db).HandleSensorReadingAsync(Reading(door: true));
        Assert.Single(db.Alerts.Where(a => a.Type == "DoorOpenedOutsideMaintenance" && a.ResolvedAt == null));
    }

    [Fact]
    public async Task Door_open_during_maintenance_window_raises_no_alert()
    {
        var db = await DbWithRoomAsync();
        // Reading is Monday 10:00; this window covers it, so the alert is suppressed.
        db.MaintenanceWindows.Add(new MaintenanceWindow
        {
            RoomId = RoomId, DayOfWeek = DayOfWeek.Monday, StartTime = new(9, 0, 0), EndTime = new(17, 0, 0)
        });
        await db.SaveChangesAsync();

        await Build(db).HandleSensorReadingAsync(Reading(door: true));

        Assert.Empty(db.Alerts.Where(a => a.Type == "DoorOpenedOutsideMaintenance"));
    }
}
