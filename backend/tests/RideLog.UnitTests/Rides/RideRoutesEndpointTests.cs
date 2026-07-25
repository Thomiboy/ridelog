using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// The all-routes coverage feed: every cycling ride's route polyline in one request, for the Rides
/// page's "where have I been" map. Public, cycling-only, routes only.
/// </summary>
public class RideRoutesEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record RideRouteDto(Guid Id, string RoutePolyline);

    private async Task ResetAndSeedAsync(params Ride[] rides)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(rides);
        await context.SaveChangesAsync();
    }

    private static int seedDay;

    private static Ride Ride(string? polyline, string sport = "ROAD_BIKING")
    {
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero).AddDays(seedDay++);
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = "admin-1",
            StartTime = start,
            EndTime = start.AddHours(2),
            Duration = TimeSpan.FromHours(2),
            DistanceMeters = 40000,
            Sport = sport,
            Source = RideSource.Polar,
            RoutePolyline = polyline,
        };
    }

    [Fact]
    public async Task Returns_every_cycling_route_with_a_polyline()
    {
        var a = Ride(polyline: "poly-a");
        var b = Ride(polyline: "poly-b");
        await ResetAndSeedAsync(
            a,
            b,
            // No GPS route: nothing to draw, so it's excluded.
            Ride(polyline: null),
            // Non-cycling: excluded everywhere.
            Ride(polyline: "poly-run", sport: "RUNNING"));

        var routes = await factory.CreateClient().GetFromJsonAsync<IReadOnlyList<RideRouteDto>>("/rides/routes");

        Assert.NotNull(routes);
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(id => id), routes!.Select(r => r.Id).OrderBy(id => id));
        Assert.Equal(new[] { "poly-a", "poly-b" }.OrderBy(p => p), routes.Select(r => r.RoutePolyline).OrderBy(p => p));
    }
}
