namespace ShellySpotter.Core.Models;

public class PingResult
{
    public int Id { get; set; }
    public int PingTargetId { get; set; }
    public PingTarget PingTarget { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool IsReachable { get; set; }
    public long? RoundTripMs { get; set; }
}
