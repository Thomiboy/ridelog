using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

public class SettingsEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record SettingsDto(int? MaxHeartRate);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Settings_are_admin_only()
    {
        var get = await factory.CreateClient().GetAsync("/settings");
        var put = await factory.CreateClient().PutAsJsonAsync("/settings", new SettingsDto(190));

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
    }

    [Fact]
    public async Task Admin_sets_and_reads_back_the_max_heart_rate()
    {
        var client = await AdminClientAsync();

        var put = await client.PutAsJsonAsync("/settings", new SettingsDto(188));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var settings = await client.GetFromJsonAsync<SettingsDto>("/settings");
        Assert.Equal(188, settings!.MaxHeartRate);
    }
}
