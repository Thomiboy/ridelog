using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Polar;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Polar;

/// <summary>Boots the API with an OAuth stub whose code exchange fails (e.g. Polar rejects the request).</summary>
public sealed class FailingOAuthApiFactory : RideLogApiFactory
{
    private sealed class FailingOAuth : IPolarOAuth
    {
        public string BuildAuthorizeUrl(string state) => $"https://flow.polar.com/oauth2/authorization?state={state}";

        public Task<PolarToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Polar rejected the token exchange.");
    }

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IPolarOAuth>();
        services.AddScoped<IPolarOAuth, FailingOAuth>();
    }
}

public class PolarCallbackEndpointTests(FailingOAuthApiFactory factory) : IClassFixture<FailingOAuthApiFactory>
{
    [Fact]
    public async Task A_failed_exchange_redirects_back_to_the_admin_page_with_an_error_flag()
    {
        // A state protected with the app's own keyring, as /polar/authorize would issue.
        var state = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Polar.OAuthState")
            .Protect("admin-1");

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync($"/polar/callback?code=bad-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin?polar=error", response.Headers.Location!.ToString());
    }
}
