using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

public class RideListEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record RideListItemDto(
        Guid Id, DateTimeOffset StartTime, double DistanceKm, double DurationMinutes,
        string Sport, double? AverageSpeedKmh, double? ElevationGainMeters);

    private sealed record PagedDto(IReadOnlyList<RideListItemDto> Items, int Page, int PageSize, int Total);

    private async Task ResetAndSeedAsync(params Ride[] rides)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(rides);
        await context.SaveChangesAsync();
    }

    private static Ride Ride(DateTimeOffset start, string sport = "ROAD_BIKING", string userId = "admin-1") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        EndTime = start.AddHours(2),
        Duration = TimeSpan.FromMinutes(118),
        DistanceMeters = 61500,
        AverageSpeedKmh = 31.3,
        ElevationGainMeters = 460,
        Sport = sport,
        Source = RideSource.Polar,
    };

    [Fact]
    public async Task Lists_rides_newest_first_for_anonymous_users()
    {
        var older = Ride(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        var newer = Ride(new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero));
        await ResetAndSeedAsync(older, newer);

        var response = await factory.CreateClient().GetAsync("/rides");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedDto>();

        Assert.Equal(2, page!.Items.Count);
        Assert.Equal(newer.Id, page.Items[0].Id);
        Assert.Equal(older.Id, page.Items[1].Id);
        Assert.Equal(61.5, page.Items[0].DistanceKm, 0.01);
        Assert.Equal(118, page.Items[0].DurationMinutes, 0.5);
    }

    [Fact]
    public async Task Excludes_non_cycling_sports_but_keeps_untagged_rides()
    {
        var cycling = Ride(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), sport: "ROAD_BIKING");
        var untagged = Ride(new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), sport: "Unknown");
        var running = Ride(new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero), sport: "RUNNING");
        await ResetAndSeedAsync(cycling, untagged, running);

        var page = await (await factory.CreateClient().GetAsync("/rides")).Content.ReadFromJsonAsync<PagedDto>();

        Assert.Equal(2, page!.Total);
        Assert.DoesNotContain(page.Items, item => item.Sport == "RUNNING");
        Assert.Contains(page.Items, item => item.Sport == "Unknown");
    }

    [Fact]
    public async Task Pages_the_results()
    {
        Ride[] rides =
        [
            Ride(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)),
            Ride(new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero)),
            Ride(new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero)),
        ];
        await ResetAndSeedAsync(rides);

        var first = await (await factory.CreateClient().GetAsync("/rides?page=1&pageSize=2")).Content.ReadFromJsonAsync<PagedDto>();
        Assert.Equal(3, first!.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(rides[2].Id, first.Items[0].Id); // newest first
        Assert.Equal(rides[1].Id, first.Items[1].Id);

        var second = await (await factory.CreateClient().GetAsync("/rides?page=2&pageSize=2")).Content.ReadFromJsonAsync<PagedDto>();
        Assert.Single(second!.Items);
        Assert.Equal(rides[0].Id, second.Items[0].Id);
    }
}
