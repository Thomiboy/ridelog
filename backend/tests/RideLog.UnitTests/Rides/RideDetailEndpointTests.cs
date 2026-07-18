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
        int? Calories, Guid? PreviousId, Guid? NextId, string? RoutePolyline);

    private static Ride CyclingRideAt(DateTimeOffset start) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "admin-1",
        StartTime = start,
        EndTime = start.AddHours(1),
        Duration = TimeSpan.FromHours(1),
        DistanceMeters = 30000,
        Sport = "ROAD_BIKING",
        Source = RideSource.Polar,
    };

    private async Task SeedRidesAsync(params Ride[] rides)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        context.Rides.AddRange(rides);
        await context.SaveChangesAsync();
    }

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
            Calories = 620,
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
        Assert.Equal(620, detail.Calories);
        Assert.Equal("ROAD_BIKING", detail.Sport);
        Assert.Equal("Polar", detail.Source);
        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", detail.RoutePolyline);
    }

    [Fact]
    public async Task Exposes_the_chronological_neighbours_of_a_ride()
    {
        var older = CyclingRideAt(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        var middle = CyclingRideAt(new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero));
        var newer = CyclingRideAt(new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero));
        await SeedRidesAsync(older, middle, newer);

        var detail = await factory.CreateClient().GetFromJsonAsync<RideDetailDto>($"/rides/{middle.Id}");

        Assert.Equal(older.Id, detail!.PreviousId); // previous = the earlier (older) ride
        Assert.Equal(newer.Id, detail.NextId); // next = the later (newer) ride
    }

    [Fact]
    public async Task Neighbours_are_null_at_the_ends()
    {
        var older = CyclingRideAt(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        var newer = CyclingRideAt(new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero));
        await SeedRidesAsync(older, newer);

        var client = factory.CreateClient();
        var oldest = await client.GetFromJsonAsync<RideDetailDto>($"/rides/{older.Id}");
        var newest = await client.GetFromJsonAsync<RideDetailDto>($"/rides/{newer.Id}");

        Assert.Null(oldest!.PreviousId); // nothing older
        Assert.Equal(newer.Id, oldest.NextId);
        Assert.Equal(older.Id, newest!.PreviousId);
        Assert.Null(newest.NextId); // nothing newer
    }

    [Fact]
    public async Task Returns_404_for_a_missing_ride()
    {
        await SeedRideAsync();

        var response = await factory.CreateClient().GetAsync($"/rides/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
