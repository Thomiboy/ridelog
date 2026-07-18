using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

public sealed class RideMaintenanceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public RideMaintenanceServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static byte[] TcxWithSummary() => Encoding.UTF8.GetBytes("""
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities>
            <Activity Sport="Biking">
              <Id>2026-06-01T08:00:00Z</Id>
              <Lap StartTime="2026-06-01T08:00:00Z">
                <TotalTimeSeconds>3000</TotalTimeSeconds>
                <DistanceMeters>30000</DistanceMeters>
                <MaximumSpeed>16.5</MaximumSpeed>
                <Calories>620</Calories>
                <Extensions>
                  <LX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                    <AvgSpeed>10.0</AvgSpeed>
                  </LX>
                </Extensions>
                <Track>
                  <Trackpoint>
                    <Time>2026-06-01T08:00:00Z</Time>
                    <Position><LatitudeDegrees>47.5</LatitudeDegrees><LongitudeDegrees>19.0</LongitudeDegrees></Position>
                    <AltitudeMeters>100</AltitudeMeters><HeartRateBpm><Value>120</Value></HeartRateBpm>
                  </Trackpoint>
                  <Trackpoint>
                    <Time>2026-06-01T09:00:00Z</Time>
                    <Position><LatitudeDegrees>47.6</LatitudeDegrees><LongitudeDegrees>19.1</LongitudeDegrees></Position>
                    <AltitudeMeters>140</AltitudeMeters><HeartRateBpm><Value>160</Value></HeartRateBpm>
                  </Trackpoint>
                </Track>
              </Lap>
            </Activity>
          </Activities>
        </TrainingCenterDatabase>
        """);

    // A Polar-synced ride with deliberately stale/derived metrics and the original TCX kept as a raw file.
    private static Ride StaleRide(string userId = "user-1", DateTimeOffset? start = null)
    {
        var startTime = start ?? new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = startTime,
            EndTime = startTime.AddHours(1),
            DistanceMeters = 1,
            Duration = TimeSpan.FromMinutes(1),
            AverageSpeedKmh = 99,
            MaximumSpeedKmh = null,
            Calories = null,
            AverageHeartRate = null,
            ElevationGainMeters = null,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
            RoutePolyline = null,
        };
        ride.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Format = RawFileFormat.Tcx,
            FileName = "exercise.tcx",
            Content = TcxWithSummary(),
            UploadedAt = DateTimeOffset.UtcNow,
        });
        return ride;
    }

    private RideMaintenanceService NewService(RideLogDbContext context) =>
        new(context, [new GpxActivityParser(), new TcxActivityParser()]);

    [Fact]
    public async Task Reprocess_updates_metrics_in_place_without_touching_identity()
    {
        var ride = StaleRide();
        Guid rideId = ride.Id;
        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.Add(ride);
            await context.SaveChangesAsync();
        }

        await using (var context = new RideLogDbContext(_options))
        {
            await NewService(context).ReprocessAsync("user-1");
        }

        await using (var verify = new RideLogDbContext(_options))
        {
            var reloaded = await verify.Rides.Include(r => r.RawFiles).SingleAsync();
            // Metrics now come from the stored TCX.
            Assert.Equal(36.0, reloaded.AverageSpeedKmh!.Value, 0.01); // LX AvgSpeed 10 m/s
            Assert.Equal(59.4, reloaded.MaximumSpeedKmh!.Value, 0.01); // MaximumSpeed 16.5 m/s
            Assert.Equal(620, reloaded.Calories);
            Assert.Equal(140, reloaded.AverageHeartRate); // (120 + 160) / 2
            Assert.Equal(30000, reloaded.DistanceMeters, 0.001);
            Assert.False(string.IsNullOrEmpty(reloaded.RoutePolyline));
            // Identity, source, timestamps, sport and the raw file are left as they were.
            Assert.Equal(rideId, reloaded.Id);
            Assert.Equal(RideSource.Polar, reloaded.Source);
            Assert.Equal("ROAD_CYCLING", reloaded.Sport); // not overwritten with the TCX "Biking" label
            Assert.Equal(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), reloaded.StartTime);
            Assert.Single(reloaded.RawFiles);
        }
    }

    [Fact]
    public async Task Reprocess_counts_processed_and_failed_and_survives_a_bad_file()
    {
        var good = StaleRide(start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));

        var broken = StaleRide(start: new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero));
        broken.RawFiles.Clear();
        broken.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Format = RawFileFormat.Tcx,
            FileName = "broken.tcx",
            Content = Encoding.UTF8.GetBytes("<TrainingCenterDatabase>not really</TrainingCenterDatabase>"),
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.AddRange(good, broken);
            await context.SaveChangesAsync();
        }

        ReprocessSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewService(context).ReprocessAsync("user-1");
        }

        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.Failed);

        await using (var verify = new RideLogDbContext(_options))
        {
            // The good ride was still updated despite the bad one throwing.
            var updated = await verify.Rides.SingleAsync(r => r.Id == good.Id);
            Assert.Equal(620, updated.Calories);
        }
    }

    [Fact]
    public async Task Delete_all_removes_the_users_rides_and_raw_files_only()
    {
        var mine1 = StaleRide(userId: "user-1", start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        var mine2 = StaleRide(userId: "user-1", start: new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero));
        var other = StaleRide(userId: "user-2", start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));

        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.AddRange(mine1, mine2, other);
            await context.SaveChangesAsync();
        }

        int deleted;
        await using (var context = new RideLogDbContext(_options))
        {
            deleted = await NewService(context).DeleteAllAsync("user-1");
        }

        Assert.Equal(2, deleted);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(0, await verify.Rides.CountAsync(r => r.UserId == "user-1"));
            Assert.Equal(1, await verify.Rides.CountAsync(r => r.UserId == "user-2"));
            // The other user's raw file survived; user-1's raw files were cascaded away.
            Assert.Equal(1, await verify.Set<RawFile>().CountAsync());
        }
    }
}
