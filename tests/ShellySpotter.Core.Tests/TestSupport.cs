using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.Services;

namespace ShellySpotter.Core.Tests;

internal static class TestSupport
{
    /// <summary>A fresh, isolated in-memory AppDbContext per call.</summary>
    public static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options);

    /// <summary>Builds a ClaimsPrincipal with the same claim names the API uses (role/sub).</summary>
    public static ClaimsPrincipal User(string role, string? sub = null)
    {
        var claims = new List<Claim> { new("role", role) };
        if (sub is not null) claims.Add(new Claim("sub", sub));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}

/// <summary>Stand-in for the Redmine-backed ticket service — records calls, no HTTP.</summary>
internal sealed class FakeTicketService : ITicketService
{
    public int CallCount { get; private set; }

    public Task<string?> CreateTicketAsync(string subject, string description)
    {
        CallCount++;
        return Task.FromResult<string?>("http://redmine.local/issues/1");
    }
}
