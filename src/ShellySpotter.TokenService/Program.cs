using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ShellySpotter.TokenService.Services;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<JwtService>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Preserve raw JWT claim names so "role"-based authorization and the
        // jti lookup work the same way they do in Core-MS.
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
            // Reject already-revoked tokens on Token-MS's own protected endpoints.
            OnTokenValidated = async ctx =>
            {
                var jwtService = ctx.HttpContext.RequestServices.GetRequiredService<JwtService>();
                var jti = ctx.Principal?.FindFirst("jti")?.Value;
                if (jti is not null && await jwtService.IsTokenBlacklistedAsync(jti))
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

// Seed default users on startup
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<UserService>();
    await userService.SeedDefaultUsersAsync();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
