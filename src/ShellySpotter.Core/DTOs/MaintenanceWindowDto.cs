namespace ShellySpotter.Core.DTOs;

public record MaintenanceWindowDto(int Id, int RoomId, DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime, string? Label);

public record CreateMaintenanceWindowRequest(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime, string? Label);
