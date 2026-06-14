using System.Net.NetworkInformation;

namespace ShellySpotter.Agent.Services;

public class PingService(ILogger<PingService> logger)
{
    public async Task<(bool IsReachable, long? RoundTripMs)> PingAsync(string ipAddress)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeout: 2000);
            if (reply.Status == IPStatus.Success)
                return (true, reply.RoundtripTime);
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ping to {Ip} failed", ipAddress);
            return (false, null);
        }
    }
}
