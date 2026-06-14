using Microsoft.Extensions.Options;
using ShellySpotter.Agent.Models;
using ShellySpotter.Agent.Services;
using ShellySpotter.Agent.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentConfig>(builder.Configuration.GetSection("Agent"));

var isDev = builder.Environment.IsDevelopment();

// In Development we accept any cert (self-signed/local). In Production the Agent
// talks to Core over real HTTPS (Let's Encrypt), so certificate validation stays on.
HttpClientHandler MakeHandler() => isDev
    ? new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator }
    : new HttpClientHandler();

// The Shelly is reached over plain HTTP on the LAN, so cert handling is moot there.
builder.Services.AddHttpClient("shelly").ConfigurePrimaryHttpMessageHandler(MakeHandler);
builder.Services.AddHttpClient("core").ConfigurePrimaryHttpMessageHandler(MakeHandler);

builder.Services.AddHttpClient();

builder.Services.AddSingleton<TokenManager>();
builder.Services.AddSingleton<PingService>();
builder.Services.AddSingleton<ShellyApiClient>();
builder.Services.AddSingleton<CoreApiClient>();

builder.Services.AddHostedService<PingWorker>();

var app = builder.Build();

// Helpers for parsing Shelly placeholder query params ({tC}, {lux}, {bat}).
// If a placeholder wasn't substituted by the device it arrives as a literal
// like "{tC}" — those fail to parse and are treated as null.
static double? ParseDouble(string? s) =>
    double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
static int? ParseInt(string? s) =>
    int.TryParse(s, out var i) ? i : null;

// ── Shelly webhook receiver ─────────────────────────────────────────────────
// The battery-powered Shelly Door/Window 2 sleeps; it wakes on a door/sensor
// event, calls this endpoint with sensor values embedded as query placeholders,
// then sleeps again. Gen1 SHDW-2 substitutes: {tC}=temp °C, {lux}, {bat}=battery %.
//
// Configure in the Shelly app → Actions:
//   Open when dark/twilight/daylight:
//     http://<agent-ip>:5000/hook/door?state=open&temp={tC}&lux={lux}&bat={bat}
//   On close:
//     http://<agent-ip>:5000/hook/door?state=close&temp={tC}&lux={lux}&bat={bat}
app.MapGet("/hook/door", async (
    string? state, string? temp, string? lux, string? bat,
    ShellyApiClient shelly,
    CoreApiClient core,
    TokenManager tokenManager,
    IOptions<AgentConfig> cfg,
    ILogger<Program> log) =>
{
    var config = cfg.Value;

    var token = await tokenManager.GetTokenAsync(config.TokenServiceUrl, config.AgentUsername, config.AgentPassword);
    if (token is null)
        return Results.StatusCode(503);

    bool doorOpen = state?.Equals("open", StringComparison.OrdinalIgnoreCase) ?? false;
    double? temperature = ParseDouble(temp);
    double? brightness = ParseDouble(lux);
    int? battery = ParseInt(bat);

    // If the device didn't send sensor values (e.g. placeholders unsupported on
    // this action), try a best-effort live read while it may still be awake.
    if (temperature is null && battery is null)
    {
        var reading = await shelly.GetReadingAsync(config.ShellyBaseUrl);
        if (reading is not null)
        {
            temperature ??= reading.Temperature;
            brightness ??= reading.Brightness;
            battery ??= reading.Battery;
        }
    }

    var report = new AgentReport(config.RoomId, temperature, doorOpen, brightness, battery, []);
    await core.SendReportAsync(config.CoreBaseUrl, token, report);
    log.LogInformation("Webhook door={Door}, temp={Temp}, battery={Bat}, lux={Lux} -> forwarded",
        doorOpen ? "OPEN" : "closed", temperature, battery, brightness);

    return Results.Ok();
});

app.MapGet("/health", () => Results.Ok("Agent alive"));

app.Run();
