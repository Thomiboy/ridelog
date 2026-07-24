using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Persistence;

/// <summary>
/// Integration-style tests against a relational (SQLite in-memory) database,
/// exercising the same EF model the SQL Server migration is generated from.
/// </summary>
public sealed class RidePersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public RidePersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static Ride NewRide(string userId = "user-1", DateTimeOffset? start = null)
    {
        var startTime = start ?? new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.FromHours(2));
        return new Ride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = startTime,
            EndTime = startTime.AddHours(2),
            DistanceMeters = 61500,
            Duration = TimeSpan.FromMinutes(118),
            AverageSpeedKmh = 31.3,
            MaximumSpeedKmh = 58.9,
            AverageHeartRate = 142,
            MaximumHeartRate = 178,
            ElevationGainMeters = 460,
            AverageCadence = 84,
            Calories = 620,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
            RoutePolyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
        };
    }

    [Fact]
    public async Task Ride_with_polyline_and_raw_file_round_trips()
    {
        var ride = NewRide();
        ride.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            UserId = ride.UserId,
            Format = RawFileFormat.Tcx,
            FileName = "polar-2026-07-12.tcx",
            Content = [0x01, 0x02, 0x03, 0x04],
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.Add(ride);
            await context.SaveChangesAsync();
        }

        await using (var context = new RideLogDbContext(_options))
        {
            var loaded = await context.Rides
                .Include(r => r.RawFiles)
                .SingleAsync(r => r.Id == ride.Id);

            Assert.Equal(ride.StartTime, loaded.StartTime);
            Assert.Equal(ride.EndTime, loaded.EndTime);
            Assert.Equal(61500, loaded.DistanceMeters);
            Assert.Equal(TimeSpan.FromMinutes(118), loaded.Duration);
            Assert.Equal(31.3, loaded.AverageSpeedKmh);
            Assert.Equal(142, loaded.AverageHeartRate);
            Assert.Equal(460, loaded.ElevationGainMeters);
            Assert.Equal(620, loaded.Calories);
            Assert.Equal("ROAD_CYCLING", loaded.Sport);
            Assert.Equal(RideSource.Polar, loaded.Source);
            Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", loaded.RoutePolyline);

            var file = Assert.Single(loaded.RawFiles);
            Assert.Equal(RawFileFormat.Tcx, file.Format);
            Assert.Equal("polar-2026-07-12.tcx", file.FileName);
            Assert.Equal([0x01, 0x02, 0x03, 0x04], file.Content);
            Assert.Equal(ride.UserId, file.UserId);
        }
    }

    [Fact]
    public async Task Ride_temperature_summary_round_trips()
    {
        var ride = NewRide();
        ride.AverageTemperatureCelsius = 15.5;
        ride.MinTemperatureCelsius = 9;
        ride.MaxTemperatureCelsius = 22;

        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.Add(ride);
            await context.SaveChangesAsync();
        }

        await using (var context = new RideLogDbContext(_options))
        {
            var loaded = await context.Rides.SingleAsync(r => r.Id == ride.Id);
            Assert.Equal(15.5, loaded.AverageTemperatureCelsius);
            Assert.Equal(9, loaded.MinTemperatureCelsius);
            Assert.Equal(22, loaded.MaxTemperatureCelsius);
        }
    }

    [Fact]
    public async Task Ride_metric_series_round_trips()
    {
        var ride = NewRide();
        ride.MetricSeries =
        [
            new MetricSample(0, 0, 100, 120),
            new MetricSample(1.5, 10, 150, null),
            new MetricSample(3.0, 20, null, 145),
        ];

        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.Add(ride);
            await context.SaveChangesAsync();
        }

        await using (var context = new RideLogDbContext(_options))
        {
            var loaded = await context.Rides.SingleAsync(r => r.Id == ride.Id);
            Assert.NotNull(loaded.MetricSeries);
            Assert.Equal(3, loaded.MetricSeries!.Count);
            Assert.Equal(new MetricSample(0, 0, 100, 120), loaded.MetricSeries[0]);
            Assert.Equal(new MetricSample(1.5, 10, 150, null), loaded.MetricSeries[1]);
            Assert.Equal(new MetricSample(3.0, 20, null, 145), loaded.MetricSeries[2]);
        }
    }

    [Fact]
    public async Task Same_user_cannot_store_two_rides_with_the_same_start_time()
    {
        var start = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.FromHours(2));

        await using var context = new RideLogDbContext(_options);
        context.Rides.Add(NewRide(start: start));
        await context.SaveChangesAsync();

        context.Rides.Add(NewRide(start: start));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Different_users_may_ride_at_the_same_time()
    {
        var start = new DateTimeOffset(2026, 7, 12, 8, 30, 0, TimeSpan.FromHours(2));

        await using var context = new RideLogDbContext(_options);
        context.Rides.Add(NewRide(userId: "user-1", start: start));
        context.Rides.Add(NewRide(userId: "user-2", start: start));

        Assert.Equal(2, await context.SaveChangesAsync());
    }
}
