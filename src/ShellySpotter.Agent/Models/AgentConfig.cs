namespace ShellySpotter.Agent.Models;

public class AgentConfig
{
    public string CoreBaseUrl { get; set; } = "https://core:8080";
    public string AgentUsername { get; set; } = "agent";
    public string AgentPassword { get; set; } = string.Empty;
    public string TokenServiceUrl { get; set; } = "https://token-ms:8080";
    public int RoomId { get; set; } = 1;
    public string ShellyBaseUrl { get; set; } = "http://192.168.1.100";
    public int PollIntervalSeconds { get; set; } = 30;
}
