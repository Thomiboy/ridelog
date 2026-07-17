using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

public class RideDetailEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record RideDetailDto(
        Guid Id, DateTimeOffset StartTime, DateTimeOffset EndTime, double DistanceKm, double DurationMinutes,
        string Sport, string Source, double? AverageSpeedKmh, double? MaximumSpeedKmh,
        int? AverageHeartRate, int? MaximumHeartRate, double? ElevationGainMeters, int? AverageCadence,
        string? RoutePolyline);

    private async Task<Ride> SeedRideAsync()
    {
        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = "admin-1",
            StartTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromMinutes(118),
            DistanceMeters = 61500,
            AverageSpeedKmh = 31.3,
            MaximumSpeedKmh = 58.9,
            AverageHeartRate = 142,
            MaximumHeartRate = 178,
            ElevationGainMeters = 460,
            AverageCadence = 84,
            Sport = "ROAD_BIKING",
            Source = RideSource.Polar,
            RoutePolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        context.Rides.Add(ride);
        await context.SaveChangesAsync();
        return ride;
    }

    [Fact]
    public async Task Returns_the_ride_detail_with_route_and_metrics_for_anonymous_users()
    {
        var ride = await SeedRideAsync();

        var response = await factory.CreateClient().GetAsync($"/rides/{ride.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<RideDetailDto>();

        Assert.Equal(ride.Id, detail!.Id);
        Assert.Equal(61.5, detail.DistanceKm, 0.01);
        Assert.Equal(118, detail.DurationMinutes, 0.5);
        Assert.Equal(178, detail.MaximumHeartRate);
        Assert.Equal(84, detail.AverageCadence);
        Assert.Equal("ROAD_BIKING", detail.Sport);
        Assert.Equal("Polar", detail.Source);
        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", detail.RoutePolyline);
    }

    [Fact]
    public async Task Returns_404_for_a_missing_ride()
    {
        await SeedRideAsync();

        var response = await factory.CreateClient().GetAsync($"/rides/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
