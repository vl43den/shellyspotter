using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.Models;

namespace ShellySpotter.Core.Services;

public class AlertService(AppDbContext db, MaintenanceWindowService maintenanceService, TicketService ticketService, ILogger<AlertService> logger)
{
    public async Task HandleSensorReadingAsync(SensorReading reading)
    {
        if (reading.DoorOpen)
            await HandleDoorOpenedAsync(reading);
    }

    private async Task HandleDoorOpenedAsync(SensorReading reading)
    {
        bool inMaintenance = await maintenanceService.IsWithinMaintenanceWindowAsync(reading.RoomId, reading.Timestamp);
        if (inMaintenance)
        {
            logger.LogInformation("Door opened in room {RoomId} during maintenance window — no alert", reading.RoomId);
            return;
        }

        // Avoid duplicate open alerts
        bool existing = await db.Alerts.AnyAsync(a =>
            a.RoomId == reading.RoomId &&
            a.Type == "DoorOpenedOutsideMaintenance" &&
            a.ResolvedAt == null);

        if (existing)
            return;

        var room = await db.Rooms.FindAsync(reading.RoomId);
        var message = $"Server room door opened outside maintenance window at {reading.Timestamp:u}";

        var ticketUrl = await ticketService.CreateTicketAsync(
            $"[URGENT] Door opened — {room?.Name ?? $"Room {reading.RoomId}"}",
            message);

        var alert = new Alert
        {
            RoomId = reading.RoomId,
            Type = "DoorOpenedOutsideMaintenance",
            Message = message,
            CreatedAt = reading.Timestamp,
            TicketUrl = ticketUrl
        };

        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        logger.LogWarning("Alert created for room {RoomId}: {Message}", reading.RoomId, message);
    }

    public async Task ResolveDoorAlertsAsync(int roomId)
    {
        var openAlerts = await db.Alerts
            .Where(a => a.RoomId == roomId && a.Type == "DoorOpenedOutsideMaintenance" && a.ResolvedAt == null)
            .ToListAsync();

        foreach (var alert in openAlerts)
            alert.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
