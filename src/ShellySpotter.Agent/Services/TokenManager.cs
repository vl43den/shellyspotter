using System.Text;
using System.Text.Json;

namespace ShellySpotter.Agent.Services;

public class TokenManager(IHttpClientFactory httpClientFactory, ILogger<TokenManager> logger)
{
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public async Task<string?> GetTokenAsync(string tokenServiceUrl, string username, string password)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            return _cachedToken;

        try
        {
            var client = httpClientFactory.CreateClient();
            var body = new { username, password };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{tokenServiceUrl}/api/auth/login", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Token login failed: {Status}", response.StatusCode);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            _cachedToken = result.GetProperty("accessToken").GetString();
            _tokenExpiry = DateTime.UtcNow.AddHours(8);
            return _cachedToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to obtain token");
            return null;
        }
    }

    public void Invalidate() => _cachedToken = null;
}
