using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// The lightweight background-map feed: the longest cycling routes (id, date, distance, polyline)
/// that the Statistics page paints behind its charts. Public, cycling-only, routes only.
/// </summary>
public class LongestRidesEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LongestRideRouteDto(Guid Id, DateTimeOffset Date, double DistanceKm, string RoutePolyline);

    private async Task ResetAndSeedAsync(params Ride[] rides)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(rides);
        await context.SaveChangesAsync();
    }

    // Distinct start times per ride: the (UserId, StartTime) index is unique. Ordering is by
    // distance, so the exact times only need to differ, not to match the distance order.
    private static int seedDay;

    private static Ride Ride(
        double km, string? polyline, string sport = "ROAD_BIKING", DateTimeOffset? start = null)
    {
        var startTime = start ?? new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero).AddDays(seedDay++);
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = "admin-1",
            StartTime = startTime,
            EndTime = startTime.AddHours(2),
            Duration = TimeSpan.FromHours(2),
            DistanceMeters = km * 1000,
            Sport = sport,
            Source = RideSource.Polar,
            RoutePolyline = polyline,
        };
    }

    [Fact]
    public async Task Returns_the_three_longest_cycling_routes_descending_by_distance()
    {
        var longest = Ride(km: 120, polyline: "poly-120");
        var second = Ride(km: 90, polyline: "poly-90");
        var third = Ride(km: 60, polyline: "poly-60");
        await ResetAndSeedAsync(
            second,
            longest,
            third,
            // Shorter cycling ride with a route: exists but is not in the top 3.
            Ride(km: 40, polyline: "poly-40"),
            // Longest of all, but no GPS route: a background map can't draw it, so it's excluded.
            Ride(km: 200, polyline: null),
            // Non-cycling: excluded everywhere.
            Ride(km: 300, polyline: "poly-run", sport: "RUNNING"));

        var routes = await factory.CreateClient().GetFromJsonAsync<IReadOnlyList<LongestRideRouteDto>>("/rides/longest?take=3");

        Assert.NotNull(routes);
        Assert.Equal(3, routes!.Count);
        Assert.Equal([longest.Id, second.Id, third.Id], routes.Select(r => r.Id));
        Assert.Equal("poly-120", routes[0].RoutePolyline);
        Assert.Equal(120, routes[0].DistanceKm, 0.01);
        Assert.Equal(longest.StartTime, routes[0].Date);
    }

    [Fact]
    public async Task Take_caps_how_many_routes_come_back()
    {
        var longest = Ride(km: 120, polyline: "poly-120");
        await ResetAndSeedAsync(longest, Ride(km: 90, polyline: "poly-90"), Ride(km: 60, polyline: "poly-60"));

        var routes = await factory.CreateClient().GetFromJsonAsync<IReadOnlyList<LongestRideRouteDto>>("/rides/longest?take=1");

        Assert.NotNull(routes);
        Assert.Equal([longest.Id], routes!.Select(r => r.Id));
    }

    [Fact]
    public async Task Returns_whatever_is_available_when_there_are_fewer_routes_than_asked_for()
    {
        var longest = Ride(km: 80, polyline: "poly-80");
        var other = Ride(km: 50, polyline: "poly-50");
        await ResetAndSeedAsync(other, longest);

        var routes = await factory.CreateClient().GetFromJsonAsync<IReadOnlyList<LongestRideRouteDto>>("/rides/longest?take=3");

        Assert.NotNull(routes);
        Assert.Equal([longest.Id, other.Id], routes!.Select(r => r.Id));
    }
}
