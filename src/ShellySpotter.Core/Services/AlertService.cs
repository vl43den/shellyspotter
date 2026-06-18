using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.Models;

namespace ShellySpotter.Core.Services;

public class AlertService(AppDbContext db, MaintenanceWindowService maintenanceService, ITicketService ticketService, ILogger<AlertService> logger)
{
    // Hysteresis margin (°C): a high-temperature alert is only resolved once the
    // reading drops this far below the threshold, to avoid alert/resolve flapping
    // when the temperature hovers right at the limit.
    private const double TemperatureHysteresis = 1.0;

    public async Task HandleSensorReadingAsync(SensorReading reading)
    {
        if (reading.DoorOpen)
            await HandleDoorOpenedAsync(reading);

        if (reading.Temperature is not null)
            await HandleTemperatureAsync(reading);
    }

    private async Task HandleTemperatureAsync(SensorReading reading)
    {
        var room = await db.Rooms.FindAsync(reading.RoomId);
        if (room is null) return;

        var temperature = reading.Temperature!.Value;
        var threshold = room.HighTemperatureThreshold;

        bool openAlertExists = await db.Alerts.AnyAsync(a =>
            a.RoomId == reading.RoomId &&
            a.Type == "TemperatureHigh" &&
            a.ResolvedAt == null);

        // Above the limit and not already alerting → raise alert + ticket.
        if (temperature > threshold)
        {
            if (openAlertExists) return;

            var message = $"Temperature {temperature:F1} °C exceeded limit of {threshold:F1} °C at {reading.Timestamp:u}";
            var ticketUrl = await ticketService.CreateTicketAsync(
                $"[URGENT] High temperature — {room.Name}",
                message);

            db.Alerts.Add(new Alert
            {
                RoomId = reading.RoomId,
                Type = "TemperatureHigh",
                Message = message,
                CreatedAt = reading.Timestamp,
                TicketUrl = ticketUrl
            });
            await db.SaveChangesAsync();
            logger.LogWarning("Temperature alert for room {RoomId}: {Message}", reading.RoomId, message);
        }
        // Back to a safe level (below threshold minus hysteresis) → resolve.
        else if (openAlertExists && temperature <= threshold - TemperatureHysteresis)
        {
            await ResolveTemperatureAlertsAsync(reading.RoomId);
        }
    }

    public async Task ResolveTemperatureAlertsAsync(int roomId)
    {
        var openAlerts = await db.Alerts
            .Where(a => a.RoomId == roomId && a.Type == "TemperatureHigh" && a.ResolvedAt == null)
            .ToListAsync();

        foreach (var alert in openAlerts)
            alert.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
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
