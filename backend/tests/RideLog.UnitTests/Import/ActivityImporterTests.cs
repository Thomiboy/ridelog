using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Import;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Import;

public sealed class ActivityImporterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public ActivityImporterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private const string StartTime = "2026-06-01T08:00:00Z";

    private static byte[] Gpx(string start = StartTime, string end = "2026-06-01T09:00:00Z") => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
          <trk><type>cycling</type><trkseg>
            <trkpt lat="47.5" lon="19.0"><ele>100</ele><time>{start}</time></trkpt>
            <trkpt lat="47.6" lon="19.1"><ele>150</ele><time>{end}</time></trkpt>
          </trkseg></trk>
        </gpx>
        """);

    private static byte[] TcxWithSummary() => Encoding.UTF8.GetBytes("""
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities>
            <Activity Sport="Biking">
              <Id>2026-06-03T07:00:00Z</Id>
              <Lap StartTime="2026-06-03T07:00:00Z">
                <TotalTimeSeconds>3600</TotalTimeSeconds>
                <DistanceMeters>30000</DistanceMeters>
                <MaximumSpeed>16.5</MaximumSpeed>
                <Calories>620</Calories>
                <Track>
                  <Trackpoint>
                    <Time>2026-06-03T07:00:00Z</Time>
                    <Position><LatitudeDegrees>47.5</LatitudeDegrees><LongitudeDegrees>19.0</LongitudeDegrees></Position>
                  </Trackpoint>
                  <Trackpoint>
                    <Time>2026-06-03T08:00:00Z</Time>
                    <Position><LatitudeDegrees>47.6</LatitudeDegrees><LongitudeDegrees>19.1</LongitudeDegrees></Position>
                  </Trackpoint>
                </Track>
              </Lap>
            </Activity>
          </Activities>
        </TrainingCenterDatabase>
        """);

    private ActivityImporter NewImporter(RideLogDbContext context) =>
        new(context, [new GpxActivityParser(), new TcxActivityParser(), new FitActivityParser()]);

    private static readonly System.DateTime FitT0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>A cycling ride 2026-06-01 08:00–09:00Z with no temperature, as if synced from Polar.</summary>
    private static Ride SeedRide(string userId = "user-1") => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = new DateTimeOffset(FitT0, TimeSpan.Zero),
        EndTime = new DateTimeOffset(FitT0.AddHours(1), TimeSpan.Zero),
        Duration = TimeSpan.FromHours(1),
        DistanceMeters = 30000,
        AverageSpeedKmh = 30,
        Sport = "ROAD_CYCLING",
        Source = RideSource.Polar,
        RoutePolyline = "_p~iF~ps|U",
    };

    private static byte[] OverlappingFit() => TestFit.Build(
    [
        (FitT0, (sbyte)10),
        (FitT0.AddMinutes(30), (sbyte)20),
        (FitT0.AddHours(1), (sbyte)15),
    ]);

    [Fact]
    public async Task A_fit_overlapping_a_ride_enriches_temperature_and_attaches_the_file()
    {
        Guid rideId;
        await using (var context = new RideLogDbContext(_options))
        {
            var ride = SeedRide();
            rideId = ride.Id;
            context.Rides.Add(ride);
            await context.SaveChangesAsync();
        }

        ImportSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewImporter(context).ImportAsync([new ActivityFile("bryton.fit", OverlappingFit())], "user-1");
        }

        Assert.Equal(ImportOutcome.Imported, Assert.Single(summary.Files).Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            // No new ride — the FIT merged into the existing one.
            var ride = await verify.Rides.Include(r => r.RawFiles).SingleAsync();
            Assert.Equal(rideId, ride.Id);

            // Temperature series 10/20/15 → avg 15, min 10, max 20.
            Assert.Equal(15, ride.AverageTemperatureCelsius!.Value, 0.01);
            Assert.Equal(10, ride.MinTemperatureCelsius!.Value, 0.01);
            Assert.Equal(20, ride.MaxTemperatureCelsius!.Value, 0.01);

            // Everything else is untouched: metrics, source, route.
            Assert.Equal(30000, ride.DistanceMeters);
            Assert.Equal(30, ride.AverageSpeedKmh);
            Assert.Equal(RideSource.Polar, ride.Source);
            Assert.Equal("_p~iF~ps|U", ride.RoutePolyline);

            // The FIT is attached as a raw file.
            var fit = Assert.Single(ride.RawFiles);
            Assert.Equal(RawFileFormat.Fit, fit.Format);
            Assert.Equal("bryton.fit", fit.FileName);
        }
    }

    [Fact]
    public async Task Imports_a_gpx_file_as_a_ride_with_route_and_raw_file()
    {
        ImportSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewImporter(context).ImportAsync([new ActivityFile("ride.gpx", Gpx())], "user-1");
        }

        Assert.Equal(ImportOutcome.Imported, Assert.Single(summary.Files).Outcome);

        await using (var context = new RideLogDbContext(_options))
        {
            var ride = await context.Rides.Include(r => r.RawFiles).SingleAsync();
            Assert.Equal("user-1", ride.UserId);
            Assert.Equal(RideSource.Import, ride.Source);
            Assert.Equal("cycling", ride.Sport);
            Assert.False(string.IsNullOrEmpty(ride.RoutePolyline));

            var raw = Assert.Single(ride.RawFiles);
            Assert.Equal(RawFileFormat.Gpx, raw.Format);
            Assert.Equal("ride.gpx", raw.FileName);
        }
    }

    [Fact]
    public async Task Builds_a_metric_series_from_the_route_on_import()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewImporter(context).ImportAsync([new ActivityFile("ride.gpx", Gpx())], "user-1");
        }

        await using (var verify = new RideLogDbContext(_options))
        {
            var ride = await verify.Rides.SingleAsync();
            Assert.NotNull(ride.MetricSeries);
            // The GPX has two points at 100 m and 150 m elevation.
            Assert.Equal([100.0, 150.0], ride.MetricSeries!.Select(s => s.ElevationMeters));
            Assert.Equal(0, ride.MetricSeries[0].DistanceKm, 0.01);
            Assert.True(ride.MetricSeries[1].DistanceKm > 0);
        }
    }

    [Fact]
    public async Task Persists_calories_and_max_speed_from_a_tcx_summary()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewImporter(context).ImportAsync([new ActivityFile("ride.tcx", TcxWithSummary())], "user-1");
        }

        await using (var verify = new RideLogDbContext(_options))
        {
            var ride = await verify.Rides.SingleAsync();
            Assert.Equal(620, ride.Calories);
            Assert.Equal(59.4, ride.MaximumSpeedKmh!.Value, 0.01); // 16.5 m/s × 3.6
        }
    }

    [Fact]
    public async Task A_fit_with_no_overlapping_ride_creates_an_import_ride_with_temperature()
    {
        // No seeded ride — the FIT stands alone.
        ImportSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewImporter(context).ImportAsync([new ActivityFile("standalone.fit", OverlappingFit())], "user-1");
        }

        Assert.Equal(ImportOutcome.Imported, Assert.Single(summary.Files).Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            var ride = await verify.Rides.Include(r => r.RawFiles).SingleAsync();
            Assert.Equal(RideSource.Import, ride.Source);
            Assert.Equal(15, ride.AverageTemperatureCelsius!.Value, 0.01);
            Assert.Equal(10, ride.MinTemperatureCelsius!.Value, 0.01);
            Assert.Equal(20, ride.MaxTemperatureCelsius!.Value, 0.01);

            var fit = Assert.Single(ride.RawFiles);
            Assert.Equal(RawFileFormat.Fit, fit.Format);
        }
    }

    [Fact]
    public async Task Re_uploading_the_same_fit_is_an_idempotent_skip()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            context.Rides.Add(SeedRide());
            await context.SaveChangesAsync();
        }

        // First FIT enriches the ride.
        await using (var context = new RideLogDbContext(_options))
        {
            await NewImporter(context).ImportAsync([new ActivityFile("bryton.fit", OverlappingFit())], "user-1");
        }

        // Second upload of a FIT onto a ride that already has one does nothing.
        ImportSummary second;
        await using (var context = new RideLogDbContext(_options))
        {
            second = await NewImporter(context).ImportAsync([new ActivityFile("bryton-again.fit", OverlappingFit())], "user-1");
        }

        Assert.Equal(ImportOutcome.Skipped, Assert.Single(second.Files).Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            var ride = await verify.Rides.Include(r => r.RawFiles).SingleAsync();
            // Still exactly one FIT attached — no duplicate.
            Assert.Single(ride.RawFiles);
            Assert.Equal(15, ride.AverageTemperatureCelsius!.Value, 0.01);
        }
    }

    [Fact]
    public async Task Re_importing_an_overlapping_ride_is_skipped()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewImporter(context).ImportAsync([new ActivityFile("ride.gpx", Gpx())], "user-1");
        }

        ImportSummary second;
        await using (var context = new RideLogDbContext(_options))
        {
            // Same time window, different file name — still the same ride.
            second = await NewImporter(context).ImportAsync([new ActivityFile("again.gpx", Gpx())], "user-1");
        }

        Assert.Equal(ImportOutcome.Skipped, Assert.Single(second.Files).Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(1, await verify.Rides.CountAsync());
        }
    }

    [Fact]
    public async Task Another_user_may_import_a_ride_at_the_same_time()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewImporter(context).ImportAsync([new ActivityFile("ride.gpx", Gpx())], "user-1");
        }

        ImportSummary other;
        await using (var context = new RideLogDbContext(_options))
        {
            other = await NewImporter(context).ImportAsync([new ActivityFile("ride.gpx", Gpx())], "user-2");
        }

        Assert.Equal(ImportOutcome.Imported, Assert.Single(other.Files).Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(2, await verify.Rides.CountAsync());
        }
    }

    [Fact]
    public async Task A_malformed_file_fails_without_aborting_the_batch()
    {
        var malformed = Encoding.UTF8.GetBytes("<gpx>not really</gpx>");

        ImportSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewImporter(context).ImportAsync(
                [new ActivityFile("broken.gpx", malformed), new ActivityFile("ride.gpx", Gpx())],
                "user-1");
        }

        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Imported);
        Assert.Equal(ImportOutcome.Failed, summary.Files[0].Outcome);
        Assert.NotNull(summary.Files[0].Error);
        Assert.Equal(ImportOutcome.Imported, summary.Files[1].Outcome);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(1, await verify.Rides.CountAsync());
        }
    }

    [Fact]
    public async Task An_unsupported_extension_fails()
    {
        ImportSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewImporter(context).ImportAsync([new ActivityFile("notes.txt", [1, 2, 3])], "user-1");
        }

        Assert.Equal(ImportOutcome.Failed, Assert.Single(summary.Files).Outcome);
    }
}
