using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// A new rider's log is genuinely empty, and the dashboard has to be able to say so rather than
/// showing zeros. It cannot work that out from what it already sends: the monthly figures are a
/// fixed two-year grid, so they are the same length for a rider with no rides and one with plenty.
/// </summary>
public sealed class DashboardEmptyLogTests : IDisposable
{
    private const string Rider = "rider-1";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public DashboardEmptyLogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<DashboardStats> DashboardAsync()
    {
        await using var context = new RideLogDbContext(_options);
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero));
        return await new GetDashboardQueryHandler(context, clock).HandleAsync(new GetDashboardQuery(Rider));
    }

    private async Task GivenRideAsync(DateTimeOffset start)
    {
        await using var context = new RideLogDbContext(_options);
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = Rider,
            StartTime = start,
            EndTime = start.AddHours(1),
            Duration = TimeSpan.FromMinutes(58),
            DistanceMeters = 30_000,
            Sport = "ROAD_BIKING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task A_rider_with_no_rides_at_all_has_an_empty_log()
    {
        Assert.False((await DashboardAsync()).HasRides);
    }

    /// <summary>
    /// The sharp case: a ride older than the two years the dashboard charts cover. Every figure on
    /// the page is zero for this rider, and none of them is evidence that the log is empty.
    /// </summary>
    [Fact]
    public async Task A_rider_whose_only_rides_predate_the_charts_does_not_have_an_empty_log()
    {
        await GivenRideAsync(new DateTimeOffset(2021, 6, 1, 8, 0, 0, TimeSpan.Zero));

        var dashboard = await DashboardAsync();

        Assert.Equal(0, dashboard.ThisYear.RideCount);
        Assert.True(dashboard.HasRides);
    }
}
