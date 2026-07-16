using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RideLog.Application.Auth;
using RideLog.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCqrs();
builder.Services.AddRideLogPersistence(
    builder.Configuration.GetConnectionString("RideLog")
        ?? throw new InvalidOperationException("Connection string 'RideLog' is missing."));
builder.Services.AddRideLogAuth(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration ('Jwt') is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep raw JWT claim names (sub, email, role) instead of remapping to legacy URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy(AdminSeedOptions.RoleName, policy => policy.RequireRole(AdminSeedOptions.RoleName)));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<RideLogInitializer>().InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Public read endpoint.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.MapPost("/auth/login", async (LoginRequest request, IAuthService auth) =>
{
    var token = await auth.LoginAsync(request.Email, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token.Token, token.ExpiresAt));
});

// Protected endpoint: proves a JWT authorizes an admin-only route. Write endpoints reuse this policy.
app.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Ok(new
    {
        email = user.FindFirstValue("email"),
        roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value),
    }))
    .RequireAuthorization(AdminSeedOptions.RoleName);

app.Run();

internal sealed record LoginRequest(string Email, string Password);
internal sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
