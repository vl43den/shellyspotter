using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShellySpotter.WebApp.Models;

namespace ShellySpotter.WebApp.Services;

public class CoreApiService(IHttpClientFactory httpClientFactory, AuthService authService)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("core");
        if (authService.CurrentUser is { } user)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return client;
    }

    public async Task<List<RoomVm>> GetRoomsAsync()
    {
        var r = await CreateClient().GetAsync("/api/rooms");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<RoomVm>>(JsonOptions) ?? [];
    }

    public async Task<SensorReadingVm?> GetLatestReadingAsync(int roomId)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/readings/latest");
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadFromJsonAsync<SensorReadingVm>(JsonOptions);
    }

    public async Task<List<AlertVm>> GetAlertsAsync(int roomId, bool openOnly = false)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/alerts?openOnly={openOnly}");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<AlertVm>>(JsonOptions) ?? [];
    }

    public async Task ResolveAlertAsync(int roomId, int alertId)
    {
        await CreateClient().PostAsync($"/api/rooms/{roomId}/alerts/{alertId}/resolve", null);
    }

    public async Task<List<PingTargetVm>> GetPingTargetsAsync(int roomId)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/ping-targets");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<PingTargetVm>>(JsonOptions) ?? [];
    }

    public async Task<bool> AddPingTargetAsync(int roomId, string name, string ip)
    {
        var body = new { name, ipAddress = ip };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await CreateClient().PostAsync($"/api/rooms/{roomId}/ping-targets", content);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePingTargetAsync(int roomId, int targetId)
    {
        var r = await CreateClient().DeleteAsync($"/api/rooms/{roomId}/ping-targets/{targetId}");
        return r.IsSuccessStatusCode;
    }

    public async Task<List<PingResultVm>> GetPingResultsAsync(int roomId, int targetId)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/ping-targets/{targetId}/results");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<PingResultVm>>(JsonOptions) ?? [];
    }

    public async Task<List<MaintenanceWindowVm>> GetMaintenanceWindowsAsync(int roomId)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/maintenance-windows");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<MaintenanceWindowVm>>(JsonOptions) ?? [];
    }

    public async Task<bool> AddMaintenanceWindowAsync(int roomId, DayOfWeek day, TimeSpan start, TimeSpan end, string? label)
    {
        var body = new { dayOfWeek = (int)day, startTime = start.ToString(@"hh\:mm\:ss"), endTime = end.ToString(@"hh\:mm\:ss"), label };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await CreateClient().PostAsync($"/api/rooms/{roomId}/maintenance-windows", content);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteMaintenanceWindowAsync(int roomId, int windowId)
    {
        var r = await CreateClient().DeleteAsync($"/api/rooms/{roomId}/maintenance-windows/{windowId}");
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> CreateRoomAsync(string name, string description, string ownerId)
    {
        var body = new { name, description, ownerId };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await CreateClient().PostAsync("/api/rooms", content);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRoomAsync(int roomId, string name, string description, double highTemperatureThreshold)
    {
        var body = new { name, description, highTemperatureThreshold };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await CreateClient().PutAsync($"/api/rooms/{roomId}", content);
        return r.IsSuccessStatusCode;
    }

    public async Task<List<SensorReadingVm>> GetReadingsAsync(int roomId, int limit = 48)
    {
        var r = await CreateClient().GetAsync($"/api/rooms/{roomId}/readings?limit={limit}");
        if (!r.IsSuccessStatusCode) return [];
        return await r.Content.ReadFromJsonAsync<List<SensorReadingVm>>(JsonOptions) ?? [];
    }
}
