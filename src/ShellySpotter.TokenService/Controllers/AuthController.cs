using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShellySpotter.TokenService.Models;
using ShellySpotter.TokenService.Services;

namespace ShellySpotter.TokenService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserService userService, JwtService jwtService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await userService.ValidateCredentialsAsync(request.Username, request.Password);
        if (user is null)
            return Unauthorized(new { message = "Invalid username or password" });

        var token = jwtService.GenerateToken(user);
        return Ok(new TokenResponse(token, "Bearer", 28800, user.Username, user.Role));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer "))
        {
            var token = authHeader["Bearer ".Length..];
            await jwtService.BlacklistTokenAsync(token);
        }
        return Ok(new { message = "Logged out" });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var success = await userService.RegisterAsync(request);
        if (!success) return Conflict(new { message = "Username already exists" });
        return Ok(new { message = "User registered" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var username = User.FindFirst("sub")?.Value;
        var role = User.FindFirst("role")?.Value;
        return Ok(new { username, role });
    }
}
