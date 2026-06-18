using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ShellySpotter.Core.Services;

public class TicketService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TicketService> logger) : ITicketService
{
    public async Task<string?> CreateTicketAsync(string subject, string description)
    {
        var baseUrl = config["Redmine:BaseUrl"];
        var apiKey = config["Redmine:ApiKey"];
        var projectId = config["Redmine:ProjectId"] ?? "shellyspotter";

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Redmine not configured, skipping ticket creation");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("redmine");
            client.DefaultRequestHeaders.Add("X-Redmine-API-Key", apiKey);

            var body = new
            {
                issue = new
                {
                    project_id = projectId,
                    subject,
                    description,
                    priority_id = 2  // high
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/issues.json", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var issueId = result.GetProperty("issue").GetProperty("id").GetInt32();
                return $"{baseUrl}/issues/{issueId}";
            }

            logger.LogWarning("Redmine ticket creation failed: {Status}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating Redmine ticket");
            return null;
        }
    }
}
