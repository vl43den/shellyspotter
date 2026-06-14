using Microsoft.Extensions.Options;
using ShellySpotter.Agent.Models;
using ShellySpotter.Agent.Services;

namespace ShellySpotter.Agent.Workers;

/// <summary>
/// Periodically pings the configured network equipment and reports the results.
/// Door/temperature data is NOT handled here — the battery-powered Shelly is
/// event-driven and pushes those via the webhook endpoint (see Program.cs).
/// </summary>
public class PingWorker(
    CoreApiClient coreClient,
    PingService pingService,
    TokenManager tokenManager,
    IOptions<AgentConfig> config,
    ILogger<PingWorker> logger) : BackgroundService
{
    private readonly AgentConfig _config = config.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_config.PollIntervalSeconds > 0 ? _config.PollIntervalSeconds : 30);
        logger.LogInformation("PingWorker started for Room {RoomId}, interval {Interval}s", _config.RoomId, interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync();
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RunCycleAsync()
    {
        var token = await tokenManager.GetTokenAsync(
            _config.TokenServiceUrl, _config.AgentUsername, _config.AgentPassword);
        if (token is null)
        {
            logger.LogWarning("Could not obtain token, skipping ping cycle");
            return;
        }

        var targets = await coreClient.GetPingTargetsAsync(_config.CoreBaseUrl, token, _config.RoomId);
        if (targets is null || targets.Count == 0)
            return;

        var results = new List<PingResultEntry>();
        foreach (var target in targets)
        {
            var (reachable, rtt) = await pingService.PingAsync(target.IpAddress);
            logger.LogInformation("Ping {Name} ({Ip}): {Result}", target.Name, target.IpAddress, reachable ? $"{rtt}ms" : "timeout");
            results.Add(new PingResultEntry(target.Id, reachable, rtt));
        }

        await coreClient.SubmitPingResultsAsync(_config.CoreBaseUrl, token, _config.RoomId, results);
    }
}
