using StackExchange.Redis;
using ShellySpotter.TokenService.Models;
using System.Text.Json;

namespace ShellySpotter.TokenService.Services;

public class UserService(IConnectionMultiplexer redis, ILogger<UserService> logger)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string UserPrefix = "user:";

    public async Task SeedDefaultUsersAsync()
    {
        await EnsureUserAsync(new RegisterRequest("admin", "admin@shellyspotter.local", "Admin1234!", "Admin"));
        await EnsureUserAsync(new RegisterRequest("employee1", "emp@shellyspotter.local", "Employee1234!", "Employee"));
        await EnsureUserAsync(new RegisterRequest("customer1", "customer@shellyspotter.local", "Customer1234!", "Customer"));
        await EnsureUserAsync(new RegisterRequest("agent", "agent@shellyspotter.local", "Agent1234!Secret", "Agent"));
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
        return JsonSerializer.Deserialize<User>(json!);
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
