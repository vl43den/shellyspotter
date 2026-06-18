namespace ShellySpotter.Core.Services;

/// <summary>
/// Creates tickets in the external ticket system (Redmine). Abstracted so the
/// alerting logic can be unit-tested without a live Redmine / HTTP call.
/// </summary>
public interface ITicketService
{
    /// <summary>Creates a ticket and returns its URL, or null if creation was skipped/failed.</summary>
    Task<string?> CreateTicketAsync(string subject, string description);
}
