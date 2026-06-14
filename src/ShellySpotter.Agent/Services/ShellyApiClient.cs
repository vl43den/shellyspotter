using System.Text.Json;

namespace ShellySpotter.Agent.Services;

public class ShellyApiClient(IHttpClientFactory httpClientFactory, ILogger<ShellyApiClient> logger)
{
    // Gen1 Shelly Door/Window 2 (SHDW-2) exposes GET /status with all sensor values.
    public async Task<ShellyReading?> GetReadingAsync(string shellyBaseUrl)
    {
        try
        {
            var client = httpClientFactory.CreateClient("shelly");
            var response = await client.GetAsync($"{shellyBaseUrl}/status");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            logger.LogInformation("Shelly raw: {Json}", json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool doorOpen = false;
            if (root.TryGetProperty("sensor", out var sensor) &&
                sensor.TryGetProperty("state", out var state))
                doorOpen = state.GetString() == "open";

            double? temp = null;
            if (root.TryGetProperty("tmp", out var tmp) &&
                tmp.TryGetProperty("value", out var tval) &&
                tval.ValueKind == JsonValueKind.Number)
                temp = tval.GetDouble();

            int? battery = null;
            if (root.TryGetProperty("bat", out var bat) &&
                bat.TryGetProperty("value", out var bval) &&
                bval.ValueKind == JsonValueKind.Number)
                battery = bval.GetInt32();

            double? lux = null;
            if (root.TryGetProperty("lux", out var luxEl) &&
                luxEl.TryGetProperty("value", out var lval) &&
                lval.ValueKind == JsonValueKind.Number)
                lux = lval.GetDouble();

            logger.LogInformation("Parsed: door={Door}, temp={Temp}, battery={Bat}, lux={Lux}", doorOpen, temp, battery, lux);
            return new ShellyReading(doorOpen, temp, battery, lux);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not read full Shelly status ({Msg}) — device may have gone back to sleep", ex.Message);
            return null;
        }
    }
}

public record ShellyReading(bool DoorOpen, double? Temperature, int? Battery, double? Brightness);
