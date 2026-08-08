using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using RideLog.Application.Auth;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// The round trip a rider actually makes: away to the provider, back to a callback, and one exchange
/// of the callback's code for a token. The provider itself is stood in for — what is under test is
/// this app's half, and the decisions it makes are the same whoever answers.
/// </summary>
public class ExternalSignInEndpointsTests(ExternalSignInEndpointsTests.Factory factory)
    : IClassFixture<ExternalSignInEndpointsTests.Factory>
{
    private const string Frontend = "https://localhost:4200";

    /// <summary>
    /// Answers as Google would, with whatever identity the test set. Nothing here decides anything:
    /// a real provider is an HTTP call away and this is the shape of what it returns.
    /// </summary>
    public sealed class StandInProviders : IExternalProviders
    {
        public ExternalIdentity? Identity { get; set; }

        public bool Knows(string provider) =>
            provider is "google" or "microsoft";

        public string BuildAuthorizeUrl(string provider, string state) =>
            $"https://accounts.{provider}.test/authorize?state={Uri.EscapeDataString(state)}";

        public Task<ExternalIdentity?> IdentityForAsync(
            string provider, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Identity);
    }

    public sealed class Factory : RideLogApiFactory
    {
        public StandInProviders Providers { get; } = new();

        protected override void ConfigureExtraServices(IServiceCollection services)
        {
            services.RemoveAll<IExternalProviders>();
            services.AddSingleton<IExternalProviders>(Providers);
        }
    }

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string QueryValue(Uri uri, string key) =>
        HttpUtility.ParseQueryString(uri.Query)[key] ?? string.Empty;

    /// <summary>Validated independently of how it was produced, so the assertion can disagree with the code.</summary>
    private static ClaimsPrincipal Validate(string token) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "ridelog-api",
            ValidAudience = "ridelog-web",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("integration-test-signing-key-32-bytes!!")),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
        }, out _);

    /// <summary>Walks the round trip and returns where the callback sent the browser.</summary>
    private async Task<Uri> SignInThroughAsync(HttpClient client, string provider)
    {
        var away = await client.GetAsync($"/auth/{provider}/authorize");
        var state = QueryValue(away.Headers.Location!, "state");

        var back = await client.GetAsync($"/auth/{provider}/callback?code=provider-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Found, back.StatusCode);
        return back.Headers.Location!;
    }

    [Fact]
    public async Task A_rider_is_sent_to_the_provider_and_comes_back_with_a_code_worth_a_token()
    {
        var client = Client();
        factory.Providers.Identity = new ExternalIdentity("google", "google-1", "new-rider@example.test", true);

        var landing = await SignInThroughAsync(client, "google");
        var exchanged = await client.PostAsJsonAsync("/auth/exchange", new { code = QueryValue(landing, "code") });

        Assert.StartsWith(Frontend, landing.ToString());
        exchanged.EnsureSuccessStatusCode();
        var token = (await exchanged.Content.ReadFromJsonAsync<AccessTokenDto>())!;
        var principal = Validate(token.Token);
        Assert.Equal("new-rider@example.test", principal.FindFirstValue(JwtRegisteredClaimNames.Email));
        Assert.False(string.IsNullOrWhiteSpace(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)));
    }

    /// <summary>
    /// The token never reaches a URL, so the code must be worth nothing twice — otherwise a shared
    /// machine's history hands the next person a sign-in.
    /// </summary>
    [Fact]
    public async Task The_code_from_the_callback_is_spent_by_the_first_exchange()
    {
        var client = Client();
        factory.Providers.Identity = new ExternalIdentity("google", "google-2", "spends-once@example.test", true);

        var landing = await SignInThroughAsync(client, "google");
        var code = QueryValue(landing, "code");

        var first = await client.PostAsJsonAsync("/auth/exchange", new { code });
        var second = await client.PostAsJsonAsync("/auth/exchange", new { code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    /// <summary>
    /// A refusal reaches the rider as a page saying so, not a 500 — the provider redirected a
    /// browser here, exactly as the Polar callback has to handle.
    /// </summary>
    [Fact]
    public async Task An_identity_the_provider_did_not_verify_comes_back_as_an_error_and_no_code()
    {
        var client = Client();
        factory.Providers.Identity = new ExternalIdentity("google", "google-3", "unverified@example.test", false);

        var landing = await SignInThroughAsync(client, "google");

        Assert.Equal(string.Empty, QueryValue(landing, "code"));
        Assert.NotEqual(string.Empty, QueryValue(landing, "error"));
    }

    /// <summary>
    /// State is what ties the code coming back to a sign-in this app started. Without the check a
    /// crafted callback link would sign a rider in wherever the attacker's provider account points.
    /// </summary>
    [Fact]
    public async Task A_callback_carrying_a_state_this_app_never_issued_is_refused()
    {
        var response = await Client().GetAsync("/auth/google/callback?code=provider-code&state=made-up");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotEqual(string.Empty, QueryValue(response.Headers.Location!, "error"));
        Assert.Equal(string.Empty, QueryValue(response.Headers.Location!, "code"));
    }

    [Fact]
    public async Task A_provider_this_app_does_not_know_is_not_a_sign_in_route()
    {
        var response = await Client().GetAsync("/auth/facebook/authorize");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A rider who signed in this way is nobody special: they can read their own profile, which is
    /// how the app knows who is signed in, and they are not an admin for having arrived.
    /// </summary>
    [Fact]
    public async Task A_rider_signed_in_by_a_provider_can_read_their_own_profile_and_is_no_admin()
    {
        var client = Client();
        factory.Providers.Identity = new ExternalIdentity("google", "google-4", "ordinary@example.test", true);

        var landing = await SignInThroughAsync(client, "google");
        var exchanged = await client.PostAsJsonAsync("/auth/exchange", new { code = QueryValue(landing, "code") });
        var token = (await exchanged.Content.ReadFromJsonAsync<AccessTokenDto>())!;

        using var me = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var profile = await client.SendAsync(me);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = (await profile.Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal("ordinary@example.test", body.Email);
        Assert.Empty(body.Roles);
    }

    private sealed record AccessTokenDto(string Token, DateTimeOffset ExpiresAt);
    private sealed record ProfileDto(string Email, IReadOnlyList<string> Roles);
}
