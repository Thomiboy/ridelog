using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using RideLog.Api;
using RideLog.Application.Auth;
using RideLog.Application.Import;
using RideLog.Application.Messaging;
using RideLog.Application.Polar;
using RideLog.Application.Rides;
using RideLog.Application.Weather;
using RideLog.Application.Users;
using RideLog.Infrastructure.Auth;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

var builder = WebApplication.CreateBuilder(args);

// Enums travel as their names. An ordinal would make the wire format depend on the order members
// happen to be declared in, so reordering them would silently change what clients receive.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
// Scan both Application and Infrastructure: query handlers that project via EF live in Infrastructure.
builder.Services.AddCqrs(typeof(GetRidesQuery).Assembly, typeof(RideLogDbContext).Assembly);
builder.Services.AddRideLogPersistence(
    builder.Configuration.GetConnectionString("RideLog")
        ?? throw new InvalidOperationException("Connection string 'RideLog' is missing."));
builder.Services.AddRideLogAuth(builder.Configuration);
builder.Services.AddRideLogImport();
builder.Services.AddRideLogPolar(builder.Configuration);
builder.Services.AddRideLogWeather();

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

// A day's worth of lookups: enough to cover new rides and chip away at the archive, small enough
// that a backfill cannot run away with the free tier's quota in one morning.
const int WeatherRidesPerSync = 25;

builder.Services.Configure<PublicLogOptions>(builder.Configuration.GetSection(PublicLogOptions.SectionName));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<RideLogInitializer>().InitializeAsync();

    // A public log nobody remembered to configure is a blank site, so the setting fills itself in
    // with the rider whose log has always been the public one. Resolved here, once the admin is
    // seeded and its id exists, which keeps reading it from an endpoint a plain property access.
    var publicLog = scope.ServiceProvider.GetRequiredService<IOptions<PublicLogOptions>>().Value;
    if (string.IsNullOrEmpty(publicLog.RiderId))
    {
        var adminEmail = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value.Email;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        publicLog.RiderId = (await users.FindByEmailAsync(adminEmail))?.Id ?? string.Empty;
    }
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

// Signed in, a rider reads their own log; otherwise the one log that is public.
static string RiderFor(ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog) =>
    user.FindFirstValue("sub") ?? publicLog.Value.RiderId;

app.MapGet("/rides", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog, int? page, int? pageSize) =>
    Results.Ok(await dispatcher.QueryAsync(new GetRidesQuery(RiderFor(user, publicLog), page ?? 1, pageSize ?? 20))));

// The longest cycling routes for the Statistics page's background map (longest first, routes only).
app.MapGet("/activities", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog, int? page, int? pageSize) =>
    Results.Ok(await dispatcher.QueryAsync(new GetOtherActivitiesQuery(RiderFor(user, publicLog), page ?? 1, pageSize ?? 20))));

app.MapGet("/rides/longest", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog, int? take) =>
    Results.Ok(await dispatcher.QueryAsync(new GetLongestRidesQuery(RiderFor(user, publicLog), take ?? 3))));

// Every cycling route for the Rides page's all-routes coverage map.
app.MapGet("/rides/routes", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog) =>
    Results.Ok(await dispatcher.QueryAsync(new GetRideRoutesQuery(RiderFor(user, publicLog)))));

app.MapGet("/rides/{id:guid}", async (Guid id, IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog) =>
    await dispatcher.QueryAsync(new GetRideQuery(id, RiderFor(user, publicLog))) is { } ride
        ? Results.Ok(ride)
        : Results.NotFound());

app.MapGet("/dashboard", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog) =>
    Results.Ok(await dispatcher.QueryAsync(new GetDashboardQuery(RiderFor(user, publicLog)))));

app.MapGet("/statistics", async (IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog) =>
    Results.Ok(await dispatcher.QueryAsync(new GetStatisticsQuery(RiderFor(user, publicLog)))));

app.MapPost("/auth/login", async (LoginRequest request, IAuthService auth) =>
{
    var token = await auth.LoginAsync(request.Email, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token.Token, token.ExpiresAt));
});

// Sign-in with a provider. New riders arrive this way and no other: nothing here sends email, so a
// local password would have neither verification nor reset (docs/adr/0007).
const string SignInStatePurpose = "ExternalSignIn.State";
var signInStateLifetime = TimeSpan.FromMinutes(10);

// A redirect rather than the URL as JSON — unlike the Polar link, whoever asks is not signed in yet,
// so this is a plain link the browser follows.
app.MapGet("/auth/{provider}/authorize", (
    string provider, IExternalProviders providers, IDataProtectionProvider protection, TimeProvider clock) =>
{
    if (!providers.Knows(provider))
    {
        return Results.NotFound();
    }

    var state = protection.CreateProtector(SignInStatePurpose)
        .Protect($"{provider}|{clock.GetUtcNow().ToUnixTimeSeconds()}");

    return Results.Redirect(providers.BuildAuthorizeUrl(provider, state));
});

app.MapGet("/auth/{provider}/callback", async (
    string provider, string? code, string? state,
    IExternalProviders providers, IExternalSignIn signIn, ISignInCodes codes,
    IDataProtectionProvider protection, TimeProvider clock, ILogger<Program> logger) =>
{
    // The provider redirected a browser here, so a refusal has to arrive as a page that says so.
    var frontend = allowedOrigins.FirstOrDefault();
    IResult BackToSignIn(string query, string whenHeadless) =>
        frontend is null ? Results.BadRequest(whenHeadless) : Results.Redirect($"{frontend.TrimEnd('/')}/login{query}");

    // State is what ties this callback to a sign-in this app started; without it a crafted link
    // signs a rider in as whoever the sender's provider account names.
    if (!IsOurState(state, provider, protection, clock, signInStateLifetime))
    {
        logger.LogWarning("A {Provider} sign-in callback carried a state this app did not issue.", provider);
        return BackToSignIn("?error=state", "Invalid sign-in state.");
    }

    ExternalIdentity? identity;
    try
    {
        identity = code is null ? null : await providers.IdentityForAsync(provider, code);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "The {Provider} code exchange failed.", provider);
        return BackToSignIn("?error=provider", "The sign-in provider could not be reached.");
    }

    var rider = identity is null ? null : await signIn.SignInAsync(identity);
    if (rider is null)
    {
        logger.LogWarning("A {Provider} sign-in was refused.", provider);
        return BackToSignIn("?error=refused", "Sign-in refused.");
    }

    return BackToSignIn($"?code={Uri.EscapeDataString(codes.Issue(rider.RiderId))}", "Signed in.");
});

// The token is handed over here rather than in the callback's URL, where it would outlive the
// sign-in in browser history — on a shared machine that loses accounts.
app.MapPost("/auth/exchange", async (ExchangeRequest request, ISignInCodes codes, IAuthService auth) =>
{
    var riderId = codes.Redeem(request.Code);
    if (riderId is null)
    {
        return Results.Unauthorized();
    }

    var token = await auth.TokenForAsync(riderId);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token.Token, token.ExpiresAt));
});

static bool IsOurState(
    string? state, string provider, IDataProtectionProvider protection, TimeProvider clock, TimeSpan lifetime)
{
    if (string.IsNullOrEmpty(state))
    {
        return false;
    }

    string unprotected;
    try
    {
        unprotected = protection.CreateProtector(SignInStatePurpose).Unprotect(state);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        return false;
    }

    var parts = unprotected.Split('|');
    return parts.Length == 2
        && string.Equals(parts[0], provider, StringComparison.OrdinalIgnoreCase)
        && long.TryParse(parts[1], out var issuedAt)
        && clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(issuedAt) < lifetime;
}

// Any signed-in rider, not just the admin: this is how the app knows who is signed in, and the roles
// it answers with are the caller's own — being told you are not an admin is not a privilege.
app.MapGet("/auth/me", (ClaimsPrincipal user) => Results.Ok(new
    {
        email = user.FindFirstValue("email"),
        roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value),
    }))
    .RequireAuthorization();

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

// Admin settings: the max heart rate that anchors the HR-zone boundaries.
app.MapGet("/settings", async (IUserSettingsService settings, ClaimsPrincipal user) =>
    Results.Ok(await settings.GetAsync(user.FindFirstValue("sub")!)))
    .RequireAuthorization(AdminSeedOptions.RoleName);

app.MapPut("/settings", async (UserSettingsDto body, IUserSettingsService settings, ClaimsPrincipal user) =>
{
    await settings.SetMaxHeartRateAsync(user.FindFirstValue("sub")!, body.MaxHeartRate);
    return Results.Ok();
})
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Admin maintenance: re-parse every ride's stored raw files to refresh metrics in place. The only
// way to fix Polar-synced rides, which AccessLink never re-serves.
app.MapPost("/rides/reprocess", async (IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    Results.Ok(await maintenance.ReprocessAsync(user.FindFirstValue("sub")!)))
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Admin re-parses a single ride's stored files; 404 when the user has no such ride.
app.MapPost("/rides/{id:guid}/reprocess", async (Guid id, IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    await maintenance.ReprocessAsync(user.FindFirstValue("sub")!, id)
        ? Results.Ok()
        : Results.NotFound())
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Admin danger action: delete every ride (and its raw files) for the user.
app.MapDelete("/rides", async (IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    Results.Ok(new { deleted = await maintenance.DeleteAllAsync(user.FindFirstValue("sub")!) }))
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Admin deletes a single ride (and its raw files); 404 when the user has no such ride.
app.MapDelete("/rides/{id:guid}", async (Guid id, IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    await maintenance.DeleteAsync(user.FindFirstValue("sub")!, id)
        ? Results.Ok()
        : Results.NotFound())
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Admin starts the Polar OAuth flow; the initiating user id is carried in a protected state value.
const string OAuthStatePurpose = "Polar.OAuthState";

app.MapGet("/polar/status", async (IPolarTokenStore tokenStore) =>
    Results.Ok(await tokenStore.GetStatusAsync()))
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Returns the Polar URL as JSON so the SPA can navigate the browser to it (a bearer-authorized
// fetch can't be a redirect the browser follows).
app.MapGet("/polar/authorize", (IPolarOAuth oauth, IDataProtectionProvider protection, ClaimsPrincipal user) =>
{
    var state = protection.CreateProtector(OAuthStatePurpose).Protect(user.FindFirstValue("sub")!);
    return Results.Ok(new { authorizeUrl = oauth.BuildAuthorizeUrl(state) });
})
    .RequireAuthorization(AdminSeedOptions.RoleName);

app.MapGet("/polar/callback", async (
    string code, string state, IPolarOAuth oauth, IPolarTokenStore tokenStore,
    IDataProtectionProvider protection, ILogger<Program> logger) =>
{
    // Polar redirected the browser here, so always send the admin back to the app's admin
    // page — with an error flag instead of a raw 500 when the exchange fails.
    var frontend = allowedOrigins.FirstOrDefault();
    string AdminUrl(string result) =>
        frontend is null ? string.Empty : $"{frontend.TrimEnd('/')}/admin?polar={result}";

    string appUserId;
    try
    {
        appUserId = protection.CreateProtector(OAuthStatePurpose).Unprotect(state);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        logger.LogWarning("Polar callback received an invalid OAuth state.");
        return frontend is null ? Results.BadRequest("Invalid OAuth state.") : Results.Redirect(AdminUrl("error"));
    }

    try
    {
        var token = await oauth.ExchangeCodeAsync(code);
        await tokenStore.SaveAsync(appUserId, token);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Polar code exchange failed.");
        return frontend is null ? Results.Problem("Polar code exchange failed.") : Results.Redirect(AdminUrl("error"));
    }

    return frontend is null
        ? Results.Ok(new { linked = true })
        : Results.Redirect(AdminUrl("linked"));
});

// Sync accepts an admin JWT (manual trigger) or the shared secret header (the cron).
app.MapPost("/sync", async (
    HttpRequest request,
    IPolarSyncService sync,
    IPolarTokenStore tokenStore,
    ClaimsPrincipal user,
    IOptions<PolarOptions> polarOptions,
    IWeatherTopUpService weatherTopUp) =>
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

    var result = await sync.SyncAsync(appUserId);

    // Weather comes after the sync has committed, never inside it: the import transaction commits
    // even when an exercise fails, so a lookup failing in there would cost the ride itself
    // (docs/adr/0005). A bounded batch also backfills the archive a little every day.
    var weather = await weatherTopUp.TopUpAsync(appUserId, max: WeatherRidesPerSync);

    return Results.Ok(new { sync = result, weather });
});

// Same operation the daily sync runs, for when the owner would rather not wait for tomorrow.
app.MapPost("/rides/weather", async (IWeatherTopUpService weatherTopUp, ClaimsPrincipal user, int? max) =>
{
    var userId = user.FindFirstValue("sub");
    return userId is null
        ? Results.Unauthorized()
        : Results.Ok(await weatherTopUp.TopUpAsync(userId, max ?? WeatherRidesPerSync));
}).RequireAuthorization(AdminSeedOptions.RoleName);

app.Run();

internal sealed record LoginRequest(string Email, string Password);
internal sealed record ExchangeRequest(string Code);
internal sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
