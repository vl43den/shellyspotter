using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ShellySpotter.WebApp.Models;

namespace ShellySpotter.WebApp.Services;

public class AuthService(IHttpClientFactory httpClientFactory, ProtectedSessionStorage sessionStorage)
{
    private const string SessionKey = "auth_user";
    public CurrentUser? CurrentUser { get; private set; }

    public bool IsLoggedIn => CurrentUser is not null;
    public bool IsEmployee => CurrentUser?.Role is "Employee" or "Admin";
    public bool IsAdmin => CurrentUser?.Role == "Admin";

    public async Task InitializeAsync()
    {
        if (CurrentUser is not null) return;
        try
        {
            var result = await sessionStorage.GetAsync<CurrentUser>(SessionKey);
            if (result.Success && result.Value is not null)
                CurrentUser = result.Value;
        }
        catch { }
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var client = httpClientFactory.CreateClient("token-ms");
        var body = new { username, password };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync("/api/auth/login", content);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result.GetProperty("accessToken").GetString() ?? string.Empty;
            var role = result.GetProperty("role").GetString() ?? "Customer";
            CurrentUser = new CurrentUser(username, role, token);
            await sessionStorage.SetAsync(SessionKey, CurrentUser);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        CurrentUser = null;
        await sessionStorage.DeleteAsync(SessionKey);
    }
}
