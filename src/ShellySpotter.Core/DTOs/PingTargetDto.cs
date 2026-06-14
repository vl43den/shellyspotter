namespace ShellySpotter.Core.DTOs;

public record PingTargetDto(int Id, int RoomId, string Name, string IpAddress, bool IsEnabled);

public record CreatePingTargetRequest(string Name, string IpAddress);

public record PingResultDto(int Id, int PingTargetId, DateTime Timestamp, bool IsReachable, long? RoundTripMs);

public record SubmitPingResultsRequest(List<PingResultEntry> Results);

public record PingResultEntry(int PingTargetId, bool IsReachable, long? RoundTripMs);
