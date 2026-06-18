namespace ShellySpotter.WebApp.Models;

public record RoomVm(int Id, string Name, string Description, string OwnerId, double HighTemperatureThreshold);

public record SensorReadingVm(
    int Id, int RoomId, DateTime Timestamp,
    double? Temperature, bool DoorOpen,
    double? Brightness, int? BatteryPercent);

public record AlertVm(
    int Id, int RoomId, string Type, string Message,
    DateTime CreatedAt, DateTime? ResolvedAt, string? TicketUrl);

public record PingTargetVm(int Id, int RoomId, string Name, string IpAddress, bool IsEnabled);

public record PingResultVm(int Id, int PingTargetId, DateTime Timestamp, bool IsReachable, long? RoundTripMs);

public record MaintenanceWindowVm(int Id, int RoomId, DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime, string? Label);

public record CurrentUser(string Username, string Role, string Token);
