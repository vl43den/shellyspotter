using ShellySpotter.WebApp.Components;
using ShellySpotter.WebApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var coreUrl = builder.Configuration["Services:CoreUrl"] ?? "http://core:8080";
var tokenUrl = builder.Configuration["Services:TokenServiceUrl"] ?? "http://token-ms:8080";

builder.Services.AddHttpClient("core", c => c.BaseAddress = new Uri(coreUrl));
builder.Services.AddHttpClient("token-ms", c => c.BaseAddress = new Uri(tokenUrl));

// Singleton so auth state is shared across the Blazor Server circuit
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CoreApiService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
