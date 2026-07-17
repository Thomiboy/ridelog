using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Polar;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Polar;

/// <summary>Boots the API with a fake Polar client so /sync runs without hitting AccessLink.</summary>
public sealed class PolarApiFactory : RideLogApiFactory
{
    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IPolarClient>();
        services.AddScoped<IPolarClient>(_ => new FakePolarClient { Transaction = null });
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
        // The cron has no JWT, so the app user comes from the stored connection.
        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPolarTokenStore>();
            await store.SaveAsync("admin-1", new PolarToken("tok", "pu-1"));
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sync-Secret", RideLogApiFactory.SyncSharedSecret);

        var response = await client.PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    [Fact]
    public async Task Admin_authorize_redirects_to_polar()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync(client));

        var response = await client.GetAsync("/polar/authorize");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://flow.polar.com/oauth2/authorization", response.Headers.Location!.ToString());
    }
}
