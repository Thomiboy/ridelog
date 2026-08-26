using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// Boots the API with a deliberately tiny login limit so the guard can be watched biting inside a
/// test run. Each test builds its own, because the limiter's window is host state: a shared fixture
/// would let whichever test ran first spend the other's permits.
/// </summary>
public sealed class ThrottledLoginApiFactory : RideLogApiFactory
{
    public const int PermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:LoginRateLimitPerWindow", PermitLimit.ToString());
        builder.UseSetting("Auth:LoginRateLimitWindowMinutes", "15");
    }
}

/// <summary>
/// The password endpoint is the way into the one account that must never be unreachable, and it is
/// also the only place a password can be guessed. Both tests are about that pair: the guard has to
/// bite, and it has to leave the door working (#186).
/// </summary>
public class LoginRateLimitTests
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

    private static Task<HttpResponseMessage> AttemptAsync(HttpClient client, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, password));

    [Fact]
    public async Task Login_attempts_past_the_limit_are_refused()
    {
        using var factory = new ThrottledLoginApiFactory();
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < ThrottledLoginApiFactory.PermitLimit; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AttemptAsync(client, "wrong-password")).StatusCode);
        }

        var refused = await AttemptAsync(client, "wrong-password");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    /// <summary>
    /// The guard counts every attempt, not just the failed ones — so the way in has to still work
    /// after a few fat-fingered tries. A limiter that locked the owner out of their own emergency
    /// exit would be worse than the brute force it prevents.
    /// </summary>
    [Fact]
    public async Task A_correct_password_within_the_limit_still_returns_a_token()
    {
        using var factory = new ThrottledLoginApiFactory();
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < ThrottledLoginApiFactory.PermitLimit - 1; attempt++)
        {
            await AttemptAsync(client, "wrong-password");
        }

        var response = await AttemptAsync(client, RideLogApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }
}
