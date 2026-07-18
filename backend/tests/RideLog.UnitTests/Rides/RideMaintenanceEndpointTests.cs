using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

public class RideMaintenanceEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record ReprocessDto(int Processed, int Failed);

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

    private async Task<Guid> SeedRideAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var adminId = (await users.FindByEmailAsync(RideLogApiFactory.AdminEmail))!.Id;

        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            // Owned by the seeded admin, so the sub-scoped maintenance endpoints act on it.
            UserId = adminId,
            StartTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            DistanceMeters = 25000,
            Duration = TimeSpan.FromHours(1),
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
        };

        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        context.Rides.Add(ride);
        await context.SaveChangesAsync();
        return ride.Id;
    }

    [Fact]
    public async Task Anonymous_reprocess_is_rejected()
    {
        var response = await factory.CreateClient().PostAsync("/rides/reprocess", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_delete_all_is_rejected()
    {
        var response = await factory.CreateClient().DeleteAsync("/rides");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_reprocess_returns_counts()
    {
        await SeedRideAsync();
        var client = await AdminClientAsync();

        var response = await client.PostAsync("/rides/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ReprocessDto>();
        // The seeded ride has no raw file to parse, so it is neither processed nor failed.
        Assert.Equal(0, summary!.Processed);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task Anonymous_delete_one_is_rejected()
    {
        var id = await SeedRideAsync();

        var response = await factory.CreateClient().DeleteAsync($"/rides/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_deletes_a_single_ride()
    {
        var id = await SeedRideAsync();
        var client = await AdminClientAsync();

        var response = await client.DeleteAsync($"/rides/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await client.GetFromJsonAsync<PagedDto>("/rides");
        Assert.Equal(0, list!.Total);
    }

    [Fact]
    public async Task Admin_deleting_an_unknown_ride_is_404()
    {
        await SeedRideAsync();
        var client = await AdminClientAsync();

        var response = await client.DeleteAsync($"/rides/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_delete_all_empties_the_rides()
    {
        await SeedRideAsync();
        var client = await AdminClientAsync();

        var response = await client.DeleteAsync("/rides");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await client.GetFromJsonAsync<PagedDto>("/rides");
        Assert.Equal(0, list!.Total);
    }

    private sealed record PagedDto(int Total);
}
