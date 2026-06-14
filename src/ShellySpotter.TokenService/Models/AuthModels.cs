namespace ShellySpotter.TokenService.Models;

public record LoginRequest(string Username, string Password);

public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string Username, string Role);

public record RegisterRequest(string Username, string Email, string Password, string Role = "Customer");
