using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RideLog.Application.Auth;
using RideLog.Application.Import;
using RideLog.Application.Messaging;
using RideLog.Application.Polar;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Auth;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// Scan both Application and Infrastructure: query handlers that project via EF live in Infrastructure.
builder.Services.AddCqrs(typeof(GetRidesQuery).Assembly, typeof(RideLogDbContext).Assembly);
builder.Services.AddRideLogPersistence(
    builder.Configuration.GetConnectionString("RideLog")
        ?? throw new InvalidOperationException("Connection string 'RideLog' is missing."));
builder.Services.AddRideLogAuth(builder.Configuration);
builder.Services.AddRideLogImport();
builder.Services.AddRideLogPolar(builder.Configuration);

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

// Public read endpoints.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("HealthCheck");

app.MapGet("/rides", async (IDispatcher dispatcher, int? page, int? pageSize) =>
    Results.Ok(await dispatcher.QueryAsync(new GetRidesQuery(page ?? 1, pageSize ?? 20))));

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

// Admin-only historical GPX/TCX bulk import; returns a per-file result.
app.MapPost("/import", async (HttpRequest request, IActivityImporter importer, ClaimsPrincipal user) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected a multipart/form-data upload.");
    }

    var form = await request.ReadFormAsync();
    var userId = user.FindFirstValue("sub")!;

    var files = new List<ActivityFile>();
    foreach (var formFile in form.Files)
    {
        using var buffer = new MemoryStream();
        await formFile.CopyToAsync(buffer);
        files.Add(new ActivityFile(formFile.FileName, buffer.ToArray()));
    }

    var summary = await importer.ImportAsync(files, userId);
    return Results.Ok(summary);
})
    .RequireAuthorization(AdminSeedOptions.RoleName)
    .DisableAntiforgery();

// Admin starts the Polar OAuth flow; the initiating user id is carried in a protected state value.
const string OAuthStatePurpose = "Polar.OAuthState";

// Returns the Polar URL as JSON so the SPA can navigate the browser to it (a bearer-authorized
// fetch can't be a redirect the browser follows).
app.MapGet("/polar/authorize", (IPolarOAuth oauth, IDataProtectionProvider protection, ClaimsPrincipal user) =>
{
    var state = protection.CreateProtector(OAuthStatePurpose).Protect(user.FindFirstValue("sub")!);
    return Results.Ok(new { authorizeUrl = oauth.BuildAuthorizeUrl(state) });
})
    .RequireAuthorization(AdminSeedOptions.RoleName);

app.MapGet("/polar/callback", async (
    string code, string state, IPolarOAuth oauth, IPolarTokenStore tokenStore, IDataProtectionProvider protection) =>
{
    string appUserId;
    try
    {
        appUserId = protection.CreateProtector(OAuthStatePurpose).Unprotect(state);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        return Results.BadRequest("Invalid OAuth state.");
    }

    var token = await oauth.ExchangeCodeAsync(code);
    await tokenStore.SaveAsync(appUserId, token);

    // Polar redirected the browser here, so send the admin back to the app's admin page.
    var frontend = allowedOrigins.FirstOrDefault();
    return frontend is null
        ? Results.Ok(new { linked = true })
        : Results.Redirect($"{frontend.TrimEnd('/')}/admin?polar=linked");
});

// Sync accepts an admin JWT (manual trigger) or the shared secret header (the cron).
app.MapPost("/sync", async (
    HttpRequest request,
    IPolarSyncService sync,
    IPolarTokenStore tokenStore,
    ClaimsPrincipal user,
    IOptions<PolarOptions> polarOptions) =>
{
    var secret = polarOptions.Value.SyncSharedSecret;
    var providedSecret = request.Headers["X-Sync-Secret"].ToString();
    var authorized = user.IsInRole(AdminSeedOptions.RoleName)
        || (!string.IsNullOrEmpty(secret) && providedSecret == secret);
    if (!authorized)
    {
        return Results.Unauthorized();
    }

    var appUserId = user.FindFirstValue("sub") ?? (await tokenStore.GetConnectionAsync())?.AppUserId;
    if (appUserId is null)
    {
        return Results.BadRequest("No Polar account is linked.");
    }

    return Results.Ok(await sync.SyncAsync(appUserId));
});

app.Run();

internal sealed record LoginRequest(string Email, string Password);
internal sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
