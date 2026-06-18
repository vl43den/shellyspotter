namespace ShellySpotter.Core.DTOs;

public record RoomDto(int Id, string Name, string Description, string OwnerId, double HighTemperatureThreshold);

public record CreateRoomRequest(string Name, string Description, string OwnerId, double HighTemperatureThreshold = 28.0);

public record UpdateRoomRequest(string Name, string Description, double HighTemperatureThreshold);
