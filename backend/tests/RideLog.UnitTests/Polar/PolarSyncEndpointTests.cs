using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Polar;
using RideLog.Application.Weather;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Polar;

/// <summary>Boots the API with a fake Polar client so /sync runs without hitting AccessLink.</summary>
public sealed class PolarApiFactory : RideLogApiFactory
{
    /// <summary>Stands in for the archive lookup; set to fail for a rider whose turn should throw.</summary>
    public sealed class StandInWeather : IWeatherTopUpService
    {
        public HashSet<string> FailsFor { get; } = [];

        public Task<WeatherTopUpSummary> TopUpAsync(string userId, int max, CancellationToken cancellationToken = default) =>
            FailsFor.Contains(userId)
                ? Task.FromException<WeatherTopUpSummary>(new HttpRequestException("archive unreachable"))
                : Task.FromResult(new WeatherTopUpSummary(0, 0, 0));
    }

    public StandInWeather Weather { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IPolarClient>();
        services.AddScoped<IPolarClient>(_ => new FakePolarClient { Transaction = null });
        services.RemoveAll<IWeatherTopUpService>();
        services.AddSingleton<IWeatherTopUpService>(Weather);
    }
}

public class PolarSyncEndpointTests(PolarApiFactory factory) : IClassFixture<PolarApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

    private async Task<string> AdminTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    [Fact]
    public async Task Sync_without_credentials_is_rejected()
    {
        var response = await factory.CreateClient().PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_jwt_authorizes_sync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync(client));

        var response = await client.PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Shared_secret_header_authorizes_the_cron()
    {
        // The cron carries no JWT, so it runs for whoever has linked.
        await GivenOnlyTheseLinksAsync(("admin-1", "pu-1"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Secret", RideLogApiFactory.SyncSharedSecret);

        var response = await client.PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The signed-in admin's rider id is the one Identity minted, not the "admin-1" these tests seed
    /// rides under — a link stored for the wrong id is not the admin's link.
    /// </summary>
    private async Task<string> AdminRiderIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return (await users.FindByEmailAsync(RideLogApiFactory.AdminEmail))!.Id;
    }

    private sealed record RiderRunDto(string RiderId, SyncSummary Summary, string? Error);
    private sealed record DailyRunDto(IReadOnlyList<RiderRunDto> Riders);

    /// <summary>
    /// These tests assert who the run covered, so they have to own the whole set of links — the
    /// class shares one database, and a link left behind by another test is another rider.
    /// </summary>
    private async Task GivenOnlyTheseLinksAsync(params (string Rider, string PolarUser)[] links)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.PolarConnections.RemoveRange(context.PolarConnections);
        await context.SaveChangesAsync();

        var store = scope.ServiceProvider.GetRequiredService<IPolarTokenStore>();
        foreach (var (rider, polarUser) in links)
        {
            await store.SaveAsync(rider, new PolarToken($"tok-{polarUser}", polarUser));
        }
    }

    /// <summary>
    /// The cron speaks for nobody in particular, so it runs for everyone who has linked. It used to
    /// take whichever connection the store returned first — with two riders, one of them would never
    /// have been synced at all.
    /// </summary>
    [Fact]
    public async Task The_cron_run_covers_every_linked_rider()
    {
        await GivenOnlyTheseLinksAsync(("admin-1", "pu-1"), ("rider-2", "pu-2"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Secret", RideLogApiFactory.SyncSharedSecret);

        var response = await client.PostAsync("/sync", null);

        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<DailyRunDto>();
        Assert.Equal(["admin-1", "rider-2"], run!.Riders.Select(rider => rider.RiderId).Order());
    }

    /// <summary>
    /// Weather is looked up per rider after their sync, so an archive outage on one rider's turn is
    /// the same species of failure as an expired token: it belongs to them, not to the run.
    /// </summary>
    [Fact]
    public async Task A_weather_failure_on_one_riders_turn_does_not_end_the_run()
    {
        await GivenOnlyTheseLinksAsync(("admin-1", "pu-1"), ("rider-3", "pu-3"));
        factory.Weather.FailsFor.Add("admin-1");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Secret", RideLogApiFactory.SyncSharedSecret);

        var response = await client.PostAsync("/sync", null);

        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<DailyRunDto>();
        Assert.Contains(run!.Riders, rider => rider.RiderId == "rider-3");
    }

    [Fact]
    public async Task Wrong_shared_secret_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Secret", "not-the-secret");

        var response = await client.PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_requires_the_admin_role()
    {
        var response = await factory.CreateClient().GetAsync("/polar/authorize");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record AuthorizeResponse(string AuthorizeUrl);
    private sealed record StatusResponse(bool Linked, DateTimeOffset? ConnectedAt, DateTimeOffset? LastSyncAt);

    [Fact]
    public async Task Status_requires_the_admin_role()
    {
        var response = await factory.CreateClient().GetAsync("/polar/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_reports_the_linked_connection_to_the_admin()
    {
        await GivenOnlyTheseLinksAsync((await AdminRiderIdAsync(), "pu-1"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync(client));

        var response = await client.GetAsync("/polar/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.True(status!.Linked);
        Assert.NotNull(status.ConnectedAt);
    }

    /// <summary>
    /// The status card answers about the rider reading it. It used to answer about the first row in
    /// the table, so a rider who had not linked would have been shown somebody else's link as theirs.
    /// </summary>
    [Fact]
    public async Task Status_answers_about_the_rider_asking_not_the_first_link_stored()
    {
        await GivenOnlyTheseLinksAsync(("someone-else", "pu-9"));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync(client));

        var status = await client.GetFromJsonAsync<StatusResponse>("/polar/status");

        Assert.False(status!.Linked);
    }

    [Fact]
    public async Task Admin_authorize_returns_the_polar_url_for_the_spa()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync(client));

        var response = await client.GetAsync("/polar/authorize");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthorizeResponse>();
        Assert.StartsWith("https://flow.polar.com/oauth2/authorization", body!.AuthorizeUrl);
    }
}
