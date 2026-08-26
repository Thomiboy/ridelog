using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using RideLog.Application.Analysis;
using RideLog.Application.Auth;
using RideLog.Application.Contact;
using RideLog.Application.Import;
using RideLog.Application.Messaging;
using RideLog.Application.Polar;
using RideLog.Application.Rides;
using RideLog.Application.Settings;
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
builder.Services.AddRideLogContact(builder.Configuration);
builder.Services.AddRideLogAnalysis(builder.Configuration);

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

// Rate limiting for the one endpoint an anonymous stranger can write to (#168). There is no rate
// limiting anywhere else today; the caps and honeypot guard content, this guards frequency. The
// limit is configurable so a bulk of tests posting in one run does not trip it.
const string ContactRateLimitPolicy = "contact";
var contactPermitLimit = builder.Configuration.GetValue<int?>("Contact:RateLimitPerWindow") ?? 5;
var contactWindowMinutes = builder.Configuration.GetValue<int?>("Contact:RateLimitWindowMinutes") ?? 10;

// The other frequency guard (#186): the password endpoint is where a password can be guessed, and
// it had none. Deliberately no account lockout to go with it — this account is the break-glass key
// (docs/adr/0007), and locking it would let anyone who knows the address keep the owner out of their
// own emergency exit. The numbers are sized for the owner, not the attacker: ~960 attempts a day
// from one address is hopeless against any real password, while leaving room to mistype one.
const string LoginRateLimitPolicy = "login";
var loginPermitLimit = builder.Configuration.GetValue<int?>("Auth:LoginRateLimitPerWindow") ?? 10;
var loginWindowMinutes = builder.Configuration.GetValue<int?>("Auth:LoginRateLimitWindowMinutes") ?? 15;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ContactRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = contactPermitLimit,
                Window = TimeSpan.FromMinutes(contactWindowMinutes),
            }));
    options.AddPolicy(LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(loginWindowMinutes),
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<RideLogInitializer>().InitializeAsync();

    // Resolve whose log is public into the singleton the hot endpoints read, once, at boot. The
    // stored setting wins first: it is what the owner last moved it to (#172), and reading it from
    // configuration alone silently returned the log to the admin on every restart. Configuration is
    // the seeding fallback, and an unset one falls back to the seeded admin — a public log nobody
    // remembered to configure is a blank site (#156), and that must not happen when nothing is stored.
    var publicLog = scope.ServiceProvider.GetRequiredService<IOptions<PublicLogOptions>>().Value;
    var stored = await scope.ServiceProvider.GetRequiredService<ISettingsStore>()
        .GetAsync(SettingsKeys.PublicLogRiderId);
    if (!string.IsNullOrEmpty(stored))
    {
        publicLog.RiderId = stored;
    }
    else if (string.IsNullOrEmpty(publicLog.RiderId))
    {
        var adminEmail = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value.Email;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
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
app.UseRateLimiter();

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

app.MapGet("/statistics", async (
    IDispatcher dispatcher, ClaimsPrincipal user, IOptions<PublicLogOptions> publicLog,
    IMonthlyAnalysisService analyses) =>
{
    var statistics = await dispatcher.QueryAsync(new GetStatisticsQuery(RiderFor(user, publicLog)));

    // The page is told whether the analysis section exists rather than left to guess — the wart the
    // login page still carries, where an unconfigured provider leaves a live-looking dead link (#186).
    // Composed here rather than inside the query: whether the app can afford to write one is a fact
    // about the app, not about this rider's month.
    return Results.Ok(statistics with { AnalysisAvailable = await analyses.IsAvailableAsync() });
});

// The monthly analysis (#187, docs/adr/0008). A rider's own month, so every route names the signed-in
// rider and there is nothing here for a visitor: unlike the rest of /statistics, this is not public.
app.MapPost("/statistics/analysis", async (
    AnalysisRequest body, ClaimsPrincipal user, IMonthlyAnalysisService analyses) =>
{
    var outcome = await analyses.WriteAsync(
        user.FindFirstValue("sub")!, body.Year, body.Month, body.Language);

    return outcome.Refusal switch
    {
        AnalysisRefusal.None => Results.Ok(outcome.Analysis),
        // The switch is enforced here, not by hiding the section (#168): a request that skips the
        // page still has to meet it.
        AnalysisRefusal.Unavailable => Results.StatusCode(StatusCodes.Status403Forbidden),
        // Named, because the page has different things to say: one asks the rider to delete first,
        // the other tells them to go and ride.
        _ => Results.Conflict(new { Refusal = outcome.Refusal.ToString() }),
    };
}).RequireAuthorization();

app.MapGet("/statistics/analysis", async (
    int year, int month, AnalysisLanguage language, ClaimsPrincipal user, IMonthlyAnalysisService analyses) =>
{
    var stored = await analyses.ReadAsync(user.FindFirstValue("sub")!, year, month, language);
    return stored is null ? Results.NotFound() : Results.Ok(stored);
}).RequireAuthorization();

app.MapDelete("/statistics/analysis/{id:guid}", async (
    Guid id, ClaimsPrincipal user, IMonthlyAnalysisService analyses) =>
    await analyses.DeleteAsync(user.FindFirstValue("sub")!, id)
        ? Results.NoContent()
        : Results.NotFound())
    .RequireAuthorization();

// The owner's kill switch, stored, so flipping it takes effect without a restart (#172).
app.MapPut("/statistics/analysis/switch", async (AnalysisSwitchRequest body, IMonthlyAnalysisService analyses) =>
{
    await analyses.SetAvailableAsync(body.Enabled);
    return Results.Ok();
}).RequireAuthorization(AdminSeedOptions.RoleName);

app.MapPost("/auth/login", async (LoginRequest request, IAuthService auth) =>
{
    var token = await auth.LoginAsync(request.Email, request.Password);
    return token is null
        ? Results.Unauthorized()
        : Results.Ok(new LoginResponse(token.Token, token.ExpiresAt));
})
    .RequireRateLimiting(LoginRateLimitPolicy);

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

    // Arriving is not being let in. A rider the owner has not approved gets no code, so there is
    // nothing to exchange and no token anywhere — the gate is here, where the code is issued.
    // Rejected riders are told the same thing as pending ones: it is not their business which.
    if (rider.Approval != Approval.Approved)
    {
        return BackToSignIn("?status=pending", "Waiting for approval.");
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

// A rider's own settings: the max heart rate that anchors their HR-zone boundaries.
app.MapGet("/settings", async (IUserSettingsService settings, ClaimsPrincipal user) =>
    Results.Ok(await settings.GetAsync(user.FindFirstValue("sub")!)))
    .RequireAuthorization();

app.MapPut("/settings", async (UserSettingsDto body, IUserSettingsService settings, ClaimsPrincipal user) =>
{
    await settings.SetMaxHeartRateAsync(user.FindFirstValue("sub")!, body.MaxHeartRate);
    return Results.Ok();
})
    .RequireAuthorization();

// Maintenance needs no role: every operation here filters on the caller's own id, so the most it
// can reach is the caller's own log. Re-parsing a ride's stored raw files is also the only way to
// fix a Polar-synced ride, which AccessLink never re-serves — withholding that is hard to justify.
app.MapPost("/rides/reprocess", async (IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    Results.Ok(await maintenance.ReprocessAsync(user.FindFirstValue("sub")!)))
    .RequireAuthorization();

// Re-parses a single ride's stored files; 404 when the rider has no such ride.
app.MapPost("/rides/{id:guid}/reprocess", async (Guid id, IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    await maintenance.ReprocessAsync(user.FindFirstValue("sub")!, id)
        ? Results.Ok()
        : Results.NotFound())
    .RequireAuthorization();

// Danger action, but only ever to the caller's own log: every ride of theirs, and its raw files.
app.MapDelete("/rides", async (IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    Results.Ok(new { deleted = await maintenance.DeleteAllAsync(user.FindFirstValue("sub")!) }))
    .RequireAuthorization();

// Deletes a single ride (and its raw files); 404 when the rider has no such ride.
app.MapDelete("/rides/{id:guid}", async (Guid id, IRideMaintenanceService maintenance, ClaimsPrincipal user) =>
    await maintenance.DeleteAsync(user.FindFirstValue("sub")!, id)
        ? Results.Ok()
        : Results.NotFound())
    .RequireAuthorization();

// The owner's side of the door: who has knocked, and who is let in. The one surface in this app
// that reaches across riders, which is what the admin role is for (docs/adr/0006).
app.MapGet("/riders", async (IRiderAccounts accounts) => Results.Ok(await accounts.ListAsync()))
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Whose rides a signed-out visitor is served. A setting rather than a role, so it lives beside the
// riders rather than following the admin flag (docs/adr/0006).
app.MapPut("/riders/public-log", async (PublicLogRequest body, IRiderAccounts accounts) =>
    await accounts.SetPublicLogAsync(body.RiderId)
        ? Results.Ok()
        : Results.Conflict("The public log has to be a rider who is approved."))
    .RequireAuthorization(AdminSeedOptions.RoleName);

app.MapPut("/riders/{id}/approval", async (
    string id, ApprovalRequest body, IRiderAccounts accounts, ClaimsPrincipal user) =>
    await accounts.SetApprovalAsync(user.FindFirstValue("sub")!, id, body.Approval) switch
    {
        ApprovalChange.Changed => Results.Ok(),
        ApprovalChange.RefusedSelf => Results.Conflict(
            "You cannot shut yourself out — there would be nobody left to let you back in."),
        ApprovalChange.RefusedPublicLog => Results.Conflict(
            "This rider is the public log. Point that setting at somebody else first."),
        _ => Results.NotFound(),
    })
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Leaving. Distinct from "delete all my rides", which is maintenance and leaves the Polar link
// delivering — this takes the rides, the link and the login together.
app.MapDelete("/account", async (IRiderAccounts accounts, ClaimsPrincipal user) =>
    await accounts.CloseAsync(user.FindFirstValue("sub")!) switch
    {
        AccountClosure.Closed => Results.Ok(),
        AccountClosure.RefusedPublicLog => Results.Conflict(
            "This account is the public log. Point that setting at another rider first."),
        _ => Results.NotFound(),
    })
    .RequireAuthorization();

// A rider links their own Polar account; the initiating rider id is carried in a protected state
// value. No role: a rider who cannot link has a log that never fills.
const string OAuthStatePurpose = "Polar.OAuthState";

app.MapGet("/polar/status", async (IPolarTokenStore tokenStore, ClaimsPrincipal user) =>
    Results.Ok(await tokenStore.GetStatusAsync(user.FindFirstValue("sub")!)))
    .RequireAuthorization();

// Returns the Polar URL as JSON so the SPA can navigate the browser to it (a bearer-authorized
// fetch can't be a redirect the browser follows).
app.MapGet("/polar/authorize", (IPolarOAuth oauth, IDataProtectionProvider protection, ClaimsPrincipal user) =>
{
    var state = protection.CreateProtector(OAuthStatePurpose).Protect(user.FindFirstValue("sub")!);
    return Results.Ok(new { authorizeUrl = oauth.BuildAuthorizeUrl(state) });
})
    .RequireAuthorization();

app.MapGet("/polar/callback", async (
    string code, string state, IPolarOAuth oauth, IPolarTokenStore tokenStore,
    IDataProtectionProvider protection, ILogger<Program> logger) =>
{
    // Polar redirected the browser here, so always send the rider back to their account page —
    // with an error flag instead of a raw 500 when the exchange fails.
    var frontend = allowedOrigins.FirstOrDefault();
    string AccountUrl(string result) =>
        frontend is null ? string.Empty : $"{frontend.TrimEnd('/')}/account?polar={result}";

    string appUserId;
    try
    {
        appUserId = protection.CreateProtector(OAuthStatePurpose).Unprotect(state);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        logger.LogWarning("Polar callback received an invalid OAuth state.");
        return frontend is null ? Results.BadRequest("Invalid OAuth state.") : Results.Redirect(AccountUrl("error"));
    }

    try
    {
        var token = await oauth.ExchangeCodeAsync(code);
        await tokenStore.SaveAsync(appUserId, token);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Polar code exchange failed.");
        return frontend is null ? Results.Problem("Polar code exchange failed.") : Results.Redirect(AccountUrl("error"));
    }

    return frontend is null
        ? Results.Ok(new { linked = true })
        : Results.Redirect(AccountUrl("linked"));
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
    // Any signed-in rider may sync themselves — it pulls their own link into their own log — or the
    // cron may sync everyone with the shared secret.
    var authorized = user.FindFirstValue("sub") is not null
        || (!string.IsNullOrEmpty(secret) && providedSecret == secret);
    if (!authorized)
    {
        return Results.Unauthorized();
    }

    // Weather comes after the sync has committed, never inside it: the import transaction commits
    // even when an exercise fails, so a lookup failing in there would cost the ride itself
    // (docs/adr/0005). A bounded batch also backfills the archive a little every day.
    var appUserId = user.FindFirstValue("sub");
    if (appUserId is not null)
    {
        var result = await sync.SyncAsync(appUserId);
        var weather = await weatherTopUp.TopUpAsync(appUserId, max: WeatherRidesPerSync);
        return Results.Ok(new { sync = result, weather });
    }

    // The cron speaks for nobody in particular, so it runs for everyone who has linked.
    var riders = await sync.SyncAllAsync();
    foreach (var rider in riders)
    {
        try
        {
            await weatherTopUp.TopUpAsync(rider.RiderId, max: WeatherRidesPerSync);
        }
        catch (Exception ex)
        {
            // An archive outage on one rider's turn is theirs, exactly as an expired token is: the
            // rides are already committed, and every rider after this one still has a turn coming.
            app.Logger.LogError(ex, "The daily weather top-up failed for rider {RiderId}.", rider.RiderId);
        }
    }

    return Results.Ok(new { riders });
});

// What the public contact page renders from: the switch, and the owner's address when the form is off.
app.MapGet("/contact", async (IContactService contact) => Results.Ok(await contact.GetConfigAsync()));

// The contact form: the one endpoint where an anonymous stranger writes a row. Public, no sign-in.
app.MapPost("/contact", async (ContactRequest body, IContactService contact) =>
{
    // A real visitor never fills the hidden honeypot field: a filled one is a bot, so drop it —
    // silently, returning success, so the bot is not told which field gave it away.
    if (!string.IsNullOrEmpty(body.Website))
    {
        return Results.Ok();
    }

    // Hard length caps, enforced here rather than left to the column: this is the one endpoint where
    // an anonymous stranger writes a row, and there is no other guard in front of it.
    if (body.Name.Length > ContactLimits.NameMax
        || body.Email.Length > ContactLimits.EmailMax
        || body.Message.Length > ContactLimits.MessageMax)
    {
        return Results.BadRequest("Name, email or message is too long.");
    }

    // The kill switch is enforced here, not by hiding the form — a bot posts straight to the endpoint.
    if (!await contact.IsAcceptingAsync())
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    await contact.SubmitAsync(new ContactSubmission(body.Name, body.Email, body.Message));
    return Results.Ok();
})
    .RequireRateLimiting(ContactRateLimitPolicy);

// The owner's kill switch. Admin-only, and stored, so flipping it takes effect without a restart.
app.MapPut("/contact/switch", async (ContactSwitchRequest body, IContactService contact) =>
{
    await contact.SetAcceptingAsync(body.Enabled);
    return Results.Ok();
})
    .RequireAuthorization(AdminSeedOptions.RoleName);

// The owner's small list of what has come in. Admin-only, like the rest of the cross-rider surface.
app.MapGet("/messages", async (IContactService contact) => Results.Ok(await contact.ListAsync()))
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Clears one stored message once the owner has read it in the mail; 404 when there is no such message.
app.MapDelete("/messages/{id:guid}", async (Guid id, IContactService contact) =>
    await contact.DeleteAsync(id) ? Results.Ok() : Results.NotFound())
    .RequireAuthorization(AdminSeedOptions.RoleName);

// Same operation the daily sync runs, for when the owner would rather not wait for tomorrow.
app.MapPost("/rides/weather", async (IWeatherTopUpService weatherTopUp, ClaimsPrincipal user, int? max) =>
{
    var userId = user.FindFirstValue("sub");
    return userId is null
        ? Results.Unauthorized()
        : Results.Ok(await weatherTopUp.TopUpAsync(userId, max ?? WeatherRidesPerSync));
}).RequireAuthorization();

app.Run();

internal sealed record LoginRequest(string Email, string Password);
internal sealed record ExchangeRequest(string Code);
internal sealed record ApprovalRequest(Approval Approval);
internal sealed record PublicLogRequest(string RiderId);
// Website is the honeypot: a real visitor never fills it, so a filled one is a bot and the submission is dropped.
internal sealed record ContactRequest(string Name, string Email, string Message, string? Website);
internal sealed record ContactSwitchRequest(bool Enabled);
internal sealed record AnalysisRequest(int Year, int Month, AnalysisLanguage Language);
internal sealed record AnalysisSwitchRequest(bool Enabled);
internal sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

// Exposed so WebApplicationFactory<Program> can boot the API in integration tests.
public partial class Program;
