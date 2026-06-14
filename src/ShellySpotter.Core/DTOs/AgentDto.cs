namespace ShellySpotter.Core.DTOs;

// Payload sent by the Shelly webhook when door opens/closes
public record ShellyWebhookPayload(string Event, bool DoorOpen, double? Temperature, int? Battery, DateTime? Timestamp);

// Full agent report (polling mode)
public record AgentReportRequest(
    int RoomId,
    double? Temperature,
    bool DoorOpen,
    double? Brightness,
    int? BatteryPercent,
    List<PingResultEntry> PingResults);
