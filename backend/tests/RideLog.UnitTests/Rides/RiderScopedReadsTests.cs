using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Every read names the rider it is for. With one rider this changes nothing on screen, which is why
/// it went unnoticed: the queries carried no rider at all and read the whole table. With two, every
/// public page would mix them (docs/adr/0006).
/// </summary>
public sealed class RiderScopedReadsTests : IDisposable
{
    private const string Rider = "rider-1";
    private const string SomeoneElse = "rider-2";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public RiderScopedReadsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task The_ride_list_answers_for_one_rider_and_not_another()
    {
        await GivenRides(
            Ride(Rider, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), 30_000),
            Ride(SomeoneElse, new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), 90_000));

        await using var context = new RideLogDbContext(_options);
        var page = await new GetRidesQueryHandler(context).HandleAsync(new GetRidesQuery(Rider));

        Assert.Equal(1, page.Total);
        Assert.Equal([30.0], page.Items.Select(item => item.DistanceKm));
    }

    /// <summary>
    /// The sharpest of the six: a ride is fetched by id, so without a rider on the query anyone who
    /// has a guid can read anyone's ride — and a ride carries the route, which for a ride that starts
    /// at the front door is an address.
    /// </summary>
    [Fact]
    public async Task A_ride_belonging_to_someone_else_is_not_found()
    {
        var theirs = Ride(SomeoneElse, new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), 90_000);
        await GivenRides(Ride(Rider, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), 30_000), theirs);

        await using var context = new RideLogDbContext(_options);
        var detail = await new GetRideQueryHandler(context).HandleAsync(new GetRideQuery(theirs.Id, Rider));

        Assert.Null(detail);
    }

    /// <summary>
    /// The same invariant across the reads that aggregate rather than list one thing. Each one used
    /// to read the whole table, so with two riders the dashboard would count someone else's
    /// kilometres, the statistics would compare heart-rate zones across people using each one's own
    /// configured maximum, and the coverage map would draw roads its viewer never rode.
    /// </summary>
    [Fact]
    public async Task Every_read_answers_for_the_rider_it_names()
    {
        await GivenRides(
            Ride(Rider, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), 30_000),
            WithRoute(Ride(SomeoneElse, new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), 90_000)),
            WithRoute(Ride(SomeoneElse, new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero), 95_000)),
            Activity(SomeoneElse, new DateTimeOffset(2026, 6, 3, 18, 0, 0, TimeSpan.Zero)));

        await using var context = new RideLogDbContext(_options);
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero));

        var dashboard = await new GetDashboardQueryHandler(context, clock).HandleAsync(new GetDashboardQuery(Rider));
        var statistics = await new GetStatisticsQueryHandler(context).HandleAsync(new GetStatisticsQuery(Rider));
        var longest = await new GetLongestRidesQueryHandler(context).HandleAsync(new GetLongestRidesQuery(Rider));
        var routes = await new GetRideRoutesQueryHandler(context).HandleAsync(new GetRideRoutesQuery(Rider));
        var activities = await new GetOtherActivitiesQueryHandler(context).HandleAsync(new GetOtherActivitiesQuery(Rider));

        // 30 km from this rider's one ride; the other rider's 185 km are somebody else's business.
        Assert.Equal(30.0, dashboard.ThisYear.DistanceKm, 0.1);
        Assert.Equal(30.0, statistics.MonthlyAggregates.Sum(month => month.DistanceKm), 0.1);
        // The other rider's rides all carry routes, so an unscoped read would have plenty to draw —
        // these are empty because they are not this rider's, not because there was nothing there.
        Assert.Empty(longest);
        Assert.Empty(routes);
        Assert.Empty(activities.Items);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>A ride with a route, so a read that draws routes has something to wrongly return.</summary>
    private static Ride WithRoute(Ride ride)
    {
        ride.RoutePolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
        return ride;
    }

    private static Ride Activity(string userId, DateTimeOffset start)
    {
        var activity = Ride(userId, start, 9_000);
        activity.Sport = "RUNNING";
        return activity;
    }

    private static Ride Ride(string userId, DateTimeOffset start, double distanceMeters) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        EndTime = start.AddHours(1),
        Duration = TimeSpan.FromMinutes(58),
        DistanceMeters = distanceMeters,
        Sport = "ROAD_BIKING",
        Source = RideSource.Polar,
    };

    private async Task GivenRides(params Ride[] rides)
    {
        await using var context = new RideLogDbContext(_options);
        context.Rides.AddRange(rides);
        await context.SaveChangesAsync();
    }
}
