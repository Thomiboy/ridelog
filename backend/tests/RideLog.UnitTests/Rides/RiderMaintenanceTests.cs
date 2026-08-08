using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Application.Auth;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Tending one's own rides is ordinary, not a privilege. Every maintenance operation already took a
/// user and filtered on it, so "reprocess all" has always meant *all of mine* — the admin role in
/// front of it only looked right because there was one rider and every ride was theirs.
/// </summary>
public class RiderMaintenanceTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record ReprocessDto(int Processed, int Failed);
    private sealed record DeletedDto(int Deleted);

    /// <summary>A signed-in rider who is nobody special — no roles at all.</summary>
    private async Task<(HttpClient Client, string RiderId)> RiderClientAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var rider = await users.FindByEmailAsync(email);
        if (rider is null)
        {
            rider = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await users.CreateAsync(rider);
        }

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .CreateToken(rider.Id, email, []);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return (client, rider.Id);
    }

    private async Task GivenRideAsync(string riderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = riderId,
            StartTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromHours(1),
            DistanceMeters = 25_000,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();
    }

    private async Task<int> RideCountAsync(string riderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        return context.Rides.Count(ride => ride.UserId == riderId);
    }

    [Fact]
    public async Task A_rider_reprocesses_their_own_rides_without_being_an_admin()
    {
        var (client, riderId) = await RiderClientAsync("tends-own@example.test");
        await GivenRideAsync(riderId);

        var response = await client.PostAsync("/rides/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<ReprocessDto>());
    }

    /// <summary>
    /// A rider who cannot link Polar has a log that never fills, which makes the whole "your log
    /// starts empty and Polar fills it" story a dead end for everyone but the admin.
    /// </summary>
    [Fact]
    public async Task A_rider_can_link_their_own_polar_account()
    {
        var (client, _) = await RiderClientAsync("links-polar@example.test");

        var status = await client.GetAsync("/polar/status");
        var authorize = await client.GetAsync("/polar/authorize");

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorize.StatusCode);
    }

    /// <summary>
    /// Syncing oneself pulls one rider's own link and lands rides in their own log, so the admin
    /// role in front of it would have left every other rider waiting for the daily cron.
    /// </summary>
    [Fact]
    public async Task A_rider_can_sync_their_own_log()
    {
        var (client, _) = await RiderClientAsync("syncs-own@example.test");

        var response = await client.PostAsync("/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The operation that reads as global. It never was: "delete all my rides" can only reach the
    /// caller's own log, which is the whole reason it needs no role.
    /// </summary>
    [Fact]
    public async Task A_riders_delete_all_empties_their_log_and_leaves_everybody_elses_alone()
    {
        var (client, riderId) = await RiderClientAsync("deletes-own@example.test");
        var (_, someoneElse) = await RiderClientAsync("keeps-theirs@example.test");
        await GivenRideAsync(riderId);
        await GivenRideAsync(someoneElse);

        var response = await client.DeleteAsync("/rides");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<DeletedDto>())!.Deleted);
        Assert.Equal(0, await RideCountAsync(riderId));
        Assert.Equal(1, await RideCountAsync(someoneElse));
    }
}
