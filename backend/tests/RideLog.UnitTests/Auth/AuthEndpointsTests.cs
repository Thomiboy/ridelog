using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RideLog.UnitTests.Auth;

public class AuthEndpointsTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

    private async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    [Fact]
    public async Task Seeded_admin_can_log_in_and_reach_a_protected_endpoint()
    {
        var client = factory.CreateClient();

        var token = await LoginAsAdminAsync(client);
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(RideLogApiFactory.AdminEmail, await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_request_to_a_protected_endpoint_is_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_to_a_public_read_endpoint_is_allowed()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
