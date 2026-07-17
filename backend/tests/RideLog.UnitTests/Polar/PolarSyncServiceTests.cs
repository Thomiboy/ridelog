using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Polar;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

public sealed class PolarSyncServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public PolarSyncServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private const string Start = "2026-06-10T06:00:00Z";
    private const string End = "2026-06-10T07:00:00Z";

    private static byte[] Gpx() => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="polar" xmlns="http://www.topografix.com/GPX/1/1">
          <trk><trkseg>
            <trkpt lat="47.5" lon="19.0"><ele>100</ele><time>{Start}</time></trkpt>
            <trkpt lat="47.6" lon="19.1"><ele>150</ele><time>{End}</time></trkpt>
          </trkseg></trk>
        </gpx>
        """);

    private static byte[] Tcx() => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities><Activity Sport="Biking">
            <Id>{Start}</Id>
            <Lap StartTime="{Start}"><DistanceMeters>28000</DistanceMeters><Track>
              <Trackpoint><Time>{Start}</Time><HeartRateBpm><Value>135</Value></HeartRateBpm><Cadence>82</Cadence></Trackpoint>
              <Trackpoint><Time>{End}</Time><HeartRateBpm><Value>165</Value></HeartRateBpm><Cadence>88</Cadence></Trackpoint>
            </Track></Lap>
          </Activity></Activities>
        </TrainingCenterDatabase>
        """);

    private FakePolarClient ClientWithOneExercise()
    {
        var client = new FakePolarClient { Transaction = new PolarTransaction("txn-1", ["https://polar/ex/1"]) };
        client.Exercises["https://polar/ex/1"] = new PolarExercise(
            "https://polar/ex/1",
            new DateTimeOffset(2026, 6, 10, 6, 0, 0, TimeSpan.Zero),
            "ROAD_BIKING");
        client.Gpx["https://polar/ex/1"] = Gpx();
        client.Tcx["https://polar/ex/1"] = Tcx();
        return client;
    }

    private PolarSyncService NewService(IPolarClient client, RideLogDbContext context) =>
        new(client, context, [new GpxActivityParser(), new TcxActivityParser()]);

    [Fact]
    public async Task Syncs_an_exercise_into_a_ride_and_commits_the_transaction()
    {
        var client = ClientWithOneExercise();

        SyncSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewService(client, context).SyncAsync("admin-1");
        }

        Assert.Equal(1, summary.Imported);
        Assert.Contains("txn-1", client.Committed);

        await using (var verify = new RideLogDbContext(_options))
        {
            var ride = await verify.Rides.Include(r => r.RawFiles).SingleAsync();
            Assert.Equal("admin-1", ride.UserId);
            Assert.Equal(RideSource.Polar, ride.Source);
            Assert.Equal("ROAD_BIKING", ride.Sport);
            Assert.False(string.IsNullOrEmpty(ride.RoutePolyline)); // route from GPX
            Assert.Equal(165, ride.MaximumHeartRate);               // HR from TCX
            Assert.Equal(28000, ride.DistanceMeters);               // distance from TCX

            Assert.Equal(2, ride.RawFiles.Count);
            Assert.Contains(ride.RawFiles, f => f.Format == RawFileFormat.Gpx);
            Assert.Contains(ride.RawFiles, f => f.Format == RawFileFormat.Tcx);
        }
    }

    [Fact]
    public async Task Re_running_sync_lands_the_ride_exactly_once()
    {
        var client = ClientWithOneExercise();

        await using (var context = new RideLogDbContext(_options))
        {
            await NewService(client, context).SyncAsync("admin-1");
        }

        // The same exercise is served again (e.g. a prior commit was lost); dedup must skip it.
        SyncSummary rerun;
        await using (var context = new RideLogDbContext(_options))
        {
            rerun = await NewService(client, context).SyncAsync("admin-1");
        }

        Assert.Equal(0, rerun.Imported);
        Assert.Equal(1, rerun.Skipped);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(1, await verify.Rides.CountAsync());
        }
    }

    [Fact]
    public async Task A_failing_exercise_does_not_block_the_others_or_the_commit()
    {
        var client = new FakePolarClient
        {
            Transaction = new PolarTransaction("txn-1", ["https://polar/ex/bad", "https://polar/ex/good"]),
        };
        client.ExerciseFactory = url => new PolarExercise(url,
            new DateTimeOffset(2026, 6, 10, 6, 0, 0, TimeSpan.Zero), "ROAD_BIKING");
        // The bad exercise has neither GPX nor TCX.
        client.Gpx["https://polar/ex/good"] = Gpx();
        client.Tcx["https://polar/ex/good"] = Tcx();

        SyncSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewService(client, context).SyncAsync("admin-1");
        }

        Assert.Equal(1, summary.Imported);
        Assert.Equal(1, summary.Failed);
        Assert.Contains("txn-1", client.Committed);

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(1, await verify.Rides.CountAsync());
        }
    }

    [Fact]
    public async Task Nothing_new_is_a_no_op()
    {
        var client = new FakePolarClient { Transaction = null };

        SyncSummary summary;
        await using (var context = new RideLogDbContext(_options))
        {
            summary = await NewService(client, context).SyncAsync("admin-1");
        }

        Assert.Equal(new SyncSummary(0, 0, 0), summary);
        Assert.Empty(client.Committed);
    }
}
