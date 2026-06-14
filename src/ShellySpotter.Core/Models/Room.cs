namespace ShellySpotter.Core.Models;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty; // username of the customer

    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<PingTarget> PingTargets { get; set; } = new List<PingTarget>();
    public ICollection<MaintenanceWindow> MaintenanceWindows { get; set; } = new List<MaintenanceWindow>();
}
