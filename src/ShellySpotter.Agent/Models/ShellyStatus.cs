using System.Text.Json.Serialization;

namespace ShellySpotter.Agent.Models;

// Shelly Gen1 Door/Window 2 (SHDW-2) — GET /status
public class ShellyGen1Status
{
    [JsonPropertyName("sensor")]
    public ShellySensor? Sensor { get; set; }

    [JsonPropertyName("tmp")]
    public ShellyTmp? Temperature { get; set; }

    [JsonPropertyName("bat")]
    public ShellyBat? Battery { get; set; }

    [JsonPropertyName("lux")]
    public ShellyLux? Lux { get; set; }
}

public class ShellySensor
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;  // "open" or "close"

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }
}

public class ShellyTmp
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }
}

public class ShellyBat
{
    [JsonPropertyName("value")]
    public int Value { get; set; }  // percentage 0-100
}

public class ShellyLux
{
    [JsonPropertyName("value")]
    public double Value { get; set; }
}
