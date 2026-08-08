using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Other activities are what the log has always stored and never shown: the runs, walks and swims
/// that arrive from the same sources as rides. They are a sibling of rides, not a filter over them,
/// so they answer on their own endpoint and the rides list is left exactly as it was (docs/adr/0004).
/// </summary>
public class OtherActivityListEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record ActivityListItemDto(Guid Id, DateTimeOffset StartTime, string Sport, string SportCategory);

    private sealed record PagedDto(IReadOnlyList<ActivityListItemDto> Items, int Page, int PageSize, int Total);

    private async Task ResetAndSeedAsync(params Ride[] activities)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(activities);
        await context.SaveChangesAsync();
    }

    private static Ride Activity(DateTimeOffset start, string sport) => new()
    {
        Id = Guid.NewGuid(),
        UserId = "admin-1",
        StartTime = start,
        EndTime = start.AddHours(1),
        Duration = TimeSpan.FromMinutes(55),
        DistanceMeters = 9000,
        Sport = sport,
        Source = RideSource.Polar,
    };

    private static DateTimeOffset At(int day) => new(2026, 6, day, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Lists_everything_that_is_not_a_ride_newest_first()
    {
        await ResetAndSeedAsync(
            Activity(At(1), "RUNNING"),
            Activity(At(2), "ROAD_BIKING"),
            Activity(At(3), "POOL_SWIMMING"),
            Activity(At(4), "Unknown"));

        var page = await factory.CreateClient().GetFromJsonAsync<PagedDto>("/activities");

        Assert.Equal(["POOL_SWIMMING", "RUNNING"], page!.Items.Select(item => item.Sport));
    }

    /// <summary>
    /// The untagged recordings of the one-time bulk import carry no sport, and they are rides — the
    /// whole reason this reads what is *not* cycling rather than listing what is. An "Unknown" here
    /// would mean the historical import had quietly moved out of the rides list.
    /// </summary>
    [Fact]
    public async Task Leaves_the_rides_list_holding_everything_it_held_before()
    {
        await ResetAndSeedAsync(
            Activity(At(1), "RUNNING"),
            Activity(At(2), "ROAD_BIKING"),
            Activity(At(3), "Unknown"));

        var rides = await factory.CreateClient().GetFromJsonAsync<PagedDto>("/rides");

        Assert.Equal(["Unknown", "ROAD_BIKING"], rides!.Items.Select(item => item.Sport));
    }

    /// <summary>
    /// The list carries the reading of each sport, not just the raw name, so callers that need to
    /// group or compare within a kind never reimplement the table that decides it.
    /// </summary>
    [Fact]
    public async Task Says_what_each_raw_sport_name_reads_as()
    {
        await ResetAndSeedAsync(Activity(At(1), "RUNNING"), Activity(At(2), "POOL_SWIMMING"));

        var page = await factory.CreateClient().GetFromJsonAsync<PagedDto>("/activities");

        Assert.Equal(["Swimming", "Running"], page!.Items.Select(item => item.SportCategory));
    }
}
