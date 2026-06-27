using StackExchange.Redis;
using ShellySpotter.TokenService.Models;
using System.Text.Json;

namespace ShellySpotter.TokenService.Services;

public class UserService(IConnectionMultiplexer redis, IConfiguration config, ILogger<UserService> logger)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string UserPrefix = "user:";

    // Seed passwords come from configuration/environment (Seed:*), never from
    // source. Demo values live only in .env / .env.example. An account whose
    // password is not configured is simply skipped.
    public async Task SeedDefaultUsersAsync()
    {
        await SeedIfConfigured("admin", "admin@shellyspotter.local", "Admin", config["Seed:AdminPassword"]);
        await SeedIfConfigured("employee1", "emp@shellyspotter.local", "Employee", config["Seed:EmployeePassword"]);
        await SeedIfConfigured("customer1", "customer@shellyspotter.local", "Customer", config["Seed:CustomerPassword"]);
        await SeedIfConfigured("agent", "agent@shellyspotter.local", "Agent", config["Seed:AgentPassword"]);
    }

    private async Task SeedIfConfigured(string username, string email, string role, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No seed password configured for {Username}; skipping", username);
            return;
        }
        await EnsureUserAsync(new RegisterRequest(username, email, password, role));
    }

    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        var user = await GetUserAsync(username);
        if (user is null) return null;
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
    }

    public async Task<User?> GetUserAsync(string username)
    {
        var json = await _db.StringGetAsync($"{UserPrefix}{username.ToLower()}");
        if (!json.HasValue) return null;
        return JsonSerializer.Deserialize<User>((string)json!);
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        var key = $"{UserPrefix}{request.Username.ToLower()}";
        if (await _db.KeyExistsAsync(key)) return false;

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role
        };

        await _db.StringSetAsync(key, JsonSerializer.Serialize(user));
        logger.LogInformation("User {Username} registered with role {Role}", request.Username, request.Role);
        return true;
    }

    private async Task EnsureUserAsync(RegisterRequest request)
    {
        var key = $"{UserPrefix}{request.Username.ToLower()}";
        if (!await _db.KeyExistsAsync(key))
            await RegisterAsync(request);
    }
}
