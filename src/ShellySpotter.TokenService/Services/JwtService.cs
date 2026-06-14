using Microsoft.IdentityModel.Tokens;
using ShellySpotter.TokenService.Models;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShellySpotter.TokenService.Services;

public class JwtService(IConfiguration config, IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string BlacklistPrefix = "blacklist:";

    private SymmetricSecurityKey SigningKey =>
        new(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));

    public string GenerateToken(User user)
    {
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task BlacklistTokenAsync(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return;

        var jwt = handler.ReadJwtToken(token);
        var jti = jwt.Id;
        var expiry = jwt.ValidTo;
        var ttl = expiry - DateTime.UtcNow;

        if (ttl > TimeSpan.Zero)
            await _db.StringSetAsync($"{BlacklistPrefix}{jti}", "1", ttl);
    }

    public async Task<bool> IsTokenBlacklistedAsync(string jti) =>
        await _db.KeyExistsAsync($"{BlacklistPrefix}{jti}");
}
