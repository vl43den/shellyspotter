namespace ShellySpotter.Core.Models;

public class Alert
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string Type { get; set; } = string.Empty;   // "DoorOpenedOutsideMaintenance", "TemperatureHigh"
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? TicketUrl { get; set; }
}
