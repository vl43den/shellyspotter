namespace ShellySpotter.Core.DTOs;

public record AlertDto(
    int Id,
    int RoomId,
    string Type,
    string Message,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? TicketUrl);
