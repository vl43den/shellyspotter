namespace ShellySpotter.Core.DTOs;

public record SensorReadingDto(
    int Id,
    int RoomId,
    DateTime Timestamp,
    double? Temperature,
    bool DoorOpen,
    double? Brightness,
    int? BatteryPercent);

public record CreateSensorReadingRequest(
    double? Temperature,
    bool DoorOpen,
    double? Brightness,
    int? BatteryPercent);
