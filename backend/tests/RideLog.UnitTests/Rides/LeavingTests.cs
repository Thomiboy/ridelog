using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;
using RideLog.Infrastructure.Rides;
using RideLog.UnitTests.Polar;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Emptying a log and leaving are different things. Deleting rides is maintenance: the Polar link
/// stays and keeps delivering, so a rider who deleted in order to leave would find their rides back
/// the next morning. That is why closing an account exists, and why the UI has to say which is which.
/// </summary>
public sealed class LeavingTests : IDisposable
{
    private const string Rider = "rider-1";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    public LeavingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private static byte[] Tcx(string start, string end) => Encoding.UTF8.GetBytes($"""
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities><Activity Sport="Biking">
            <Id>{start}</Id>
            <Lap StartTime="{start}"><DistanceMeters>28000</DistanceMeters><Track>
              <Trackpoint><Time>{start}</Time><HeartRateBpm><Value>135</Value></HeartRateBpm></Trackpoint>
              <Trackpoint><Time>{end}</Time><HeartRateBpm><Value>165</Value></HeartRateBpm></Trackpoint>
            </Track></Lap>
          </Activity></Activities>
        </TrainingCenterDatabase>
        """);

    private static FakePolarClient ClientServing(string url, DateTimeOffset start)
    {
        var client = new FakePolarClient { Transaction = new PolarTransaction("txn-1", [url]) };
        client.Exercises[url] = new PolarExercise(url, start, "ROAD_BIKING");
        client.Tcx[url] = Tcx($"{start:yyyy-MM-ddTHH:mm:ssZ}", $"{start.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}");
        return client;
    }

    private async Task SyncAsync(FakePolarClient client)
    {
        await using var context = new RideLogDbContext(_options);
        await new PolarSyncService(
            client,
            new PolarTokenStore(context, _protection),
            context,
            [new GpxActivityParser(), new TcxActivityParser()],
            NullLogger<PolarSyncService>.Instance).SyncAsync(Rider);
    }

    private async Task<int> DeleteAllRidesAsync()
    {
        await using var context = new RideLogDbContext(_options);
        return await new RideMaintenanceService(
            context, [new GpxActivityParser(), new TcxActivityParser()]).DeleteAllAsync(Rider);
    }

    /// <summary>
    /// The trap this slice exists to name: emptying the log is not leaving. The link survives, so
    /// the very next sync starts refilling — which is right, and has to be said out loud.
    /// </summary>
    [Fact]
    public async Task Deleting_every_ride_leaves_the_link_and_the_next_sync_refills_the_log()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await new PolarTokenStore(context, _protection).SaveAsync(Rider, new PolarToken("tok", "pu-1"));
        }
        await SyncAsync(ClientServing("https://polar/ex/1", new DateTimeOffset(2026, 6, 10, 6, 0, 0, TimeSpan.Zero)));

        Assert.Equal(1, await DeleteAllRidesAsync());

        // Polar serves a later exercise, as it would the morning after.
        await SyncAsync(ClientServing("https://polar/ex/2", new DateTimeOffset(2026, 6, 11, 6, 0, 0, TimeSpan.Zero)));

        await using var verify = new RideLogDbContext(_options);
        Assert.Equal(1, await verify.Rides.CountAsync(ride => ride.UserId == Rider));
        Assert.True(await verify.PolarConnections.AnyAsync(link => link.UserId == Rider));
    }
}
