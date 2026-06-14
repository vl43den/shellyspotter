namespace ShellySpotter.Core.Models;

public class PingTarget
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public ICollection<PingResult> PingResults { get; set; } = new List<PingResult>();
}
