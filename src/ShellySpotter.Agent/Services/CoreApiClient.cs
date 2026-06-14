using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ShellySpotter.Agent.Models;

namespace ShellySpotter.Agent.Services;

public class CoreApiClient(IHttpClientFactory httpClientFactory, ILogger<CoreApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<bool> SendReportAsync(string coreBaseUrl, string token, AgentReport report)
    {
        try
        {
            var client = httpClientFactory.CreateClient("core");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(report, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{coreBaseUrl}/api/agent/report", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Core-MS rejected report: {Status}", response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send report to Core-MS");
            return false;
        }
    }

    public async Task<bool> SubmitPingResultsAsync(string coreBaseUrl, string token, int roomId, List<PingResultEntry> results)
    {
        try
        {
            var client = httpClientFactory.CreateClient("core");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(new { results }, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{coreBaseUrl}/api/rooms/{roomId}/ping-targets/ping-results", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit ping results to Core-MS");
            return false;
        }
    }

    public async Task<List<PingTargetInfo>?> GetPingTargetsAsync(string coreBaseUrl, string token, int roomId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("core");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync($"{coreBaseUrl}/api/agent/rooms/{roomId}/targets");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PingTargetInfo>>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch ping targets");
            return null;
        }
    }
}

public record AgentReport(
    int RoomId,
    double? Temperature,
    bool DoorOpen,
    double? Brightness,
    int? BatteryPercent,
    List<PingResultEntry> PingResults);

public record PingResultEntry(int PingTargetId, bool IsReachable, long? RoundTripMs);

public record PingTargetInfo(int Id, int RoomId, string Name, string IpAddress, bool IsEnabled);
