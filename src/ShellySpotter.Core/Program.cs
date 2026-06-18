using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShellySpotter.Core.Data;
using ShellySpotter.Core.Services;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<MaintenanceWindowService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<RoomAccessService>();

builder.Services.AddHttpClient("redmine");

// Core shares the Token-MS Redis instance solely to honour the logout blacklist.
// Optional: when no Redis is configured (e.g. local `dotnet run` without Docker)
// the blacklist check is skipped rather than failing every request.
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the raw JWT claim names ("sub", "role", "jti") instead of letting
        // the handler rewrite them to the long ClaimTypes URIs. Without this the
        // identity checks below (User.FindFirst("sub")) silently return null.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            // Enforce the logout blacklist: a token whose jti was revoked is
            // rejected even though it is still cryptographically valid.
            OnTokenValidated = async ctx =>
            {
                var redis = ctx.HttpContext.RequestServices.GetService<IConnectionMultiplexer>();
                if (redis is null) return;

                var jti = ctx.Principal?.FindFirst("jti")?.Value;
                if (jti is not null && await redis.GetDatabase().KeyExistsAsync($"blacklist:{jti}"))
                    ctx.Fail("Token has been revoked");
            }
        };
    });

var allowedOrigin = builder.Configuration["AllowedCorsOrigin"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
    {
        if (string.IsNullOrWhiteSpace(allowedOrigin))
            p.AllowAnyOrigin();                       // development
        else
            p.WithOrigins(allowedOrigin).AllowCredentials();  // production
        p.AllowAnyHeader().AllowAnyMethod();
    }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
