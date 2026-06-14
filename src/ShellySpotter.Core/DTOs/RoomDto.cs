namespace ShellySpotter.Core.DTOs;

public record RoomDto(int Id, string Name, string Description, string OwnerId);

public record CreateRoomRequest(string Name, string Description, string OwnerId);

public record UpdateRoomRequest(string Name, string Description);
