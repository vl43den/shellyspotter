namespace ShellySpotter.Core.Models;

public class SensorReading
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public double? Temperature { get; set; }
    public bool DoorOpen { get; set; }
    public double? Brightness { get; set; }
    public int? BatteryPercent { get; set; }
}
