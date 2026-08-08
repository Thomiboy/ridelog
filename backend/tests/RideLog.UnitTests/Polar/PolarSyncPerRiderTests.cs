using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

/// <summary>
/// A sync is for one rider, and it has to pull with that rider's own Polar link. Nothing said so
/// before: the API client asked the store for "the connection" and got whichever row came first, so
/// a second rider's sync would have pulled the first rider's exercises and stored them as their own.
/// </summary>
public sealed class PolarSyncPerRiderTests : IDisposable
{
    private const string Start = "2026-06-10T06:00:00Z";
    private const string End = "2026-06-10T07:00:00Z";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    public PolarSyncPerRiderTests()
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

    private static FakePolarClient ClientWithOneExerciseFor(string rider)
    {
        var url = $"https://polar/{rider}/ex/1";
        var client = new FakePolarClient { Transaction = new PolarTransaction($"txn-{rider}", [url]) };
        client.Exercises[url] = new PolarExercise(url, new DateTimeOffset(2026, 6, 10, 6, 0, 0, TimeSpan.Zero), "ROAD_BIKING");
        client.Tcx[url] = Tcx(Start, End);
        return client;
    }

    private async Task GivenLinkAsync(string rider, string accessToken, string polarUserId)
    {
        await using var context = new RideLogDbContext(_options);
        await new PolarTokenStore(context, _protection)
            .SaveAsync(rider, new PolarToken(accessToken, polarUserId));
    }

    /// <summary>One client serving a different exercise to each rider's link, as Polar would.</summary>
    private static FakePolarClient ClientPerRider()
    {
        var client = new FakePolarClient();
        foreach (var (polarUser, hour) in new[] { ("polar-1", 6), ("polar-2", 9) })
        {
            var url = $"https://polar/{polarUser}/ex/1";
            var start = new DateTimeOffset(2026, 6, 10, hour, 0, 0, TimeSpan.Zero);
            client.TransactionsByPolarUser[polarUser] = new PolarTransaction($"txn-{polarUser}", [url]);
            client.Exercises[url] = new PolarExercise(url, start, "ROAD_BIKING");
            client.Tcx[url] = Tcx($"{start:yyyy-MM-ddTHH:mm:ssZ}", $"{start.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}");
        }

        return client;
    }

    private PolarSyncService NewService(FakePolarClient client, RideLogDbContext context) =>
        new(client,
            new PolarTokenStore(context, _protection),
            context,
            [new GpxActivityParser(), new TcxActivityParser()],
            NullLogger<PolarSyncService>.Instance);

    private async Task<SyncSummary> SyncAsync(FakePolarClient client, string rider)
    {
        await using var context = new RideLogDbContext(_options);
        return await NewService(client, context).SyncAsync(rider);
    }

    private async Task<IReadOnlyList<RiderSyncResult>> SyncAllAsync(FakePolarClient client)
    {
        await using var context = new RideLogDbContext(_options);
        return await NewService(client, context).SyncAllAsync();
    }

    [Fact]
    public async Task Each_rider_is_pulled_with_their_own_link()
    {
        await GivenLinkAsync("rider-1", "token-for-one", "polar-1");
        await GivenLinkAsync("rider-2", "token-for-two", "polar-2");

        var first = ClientWithOneExerciseFor("rider-1");
        var second = ClientWithOneExerciseFor("rider-2");
        await SyncAsync(first, "rider-1");
        await SyncAsync(second, "rider-2");

        Assert.Equal(new PolarToken("token-for-one", "polar-1"), first.PulledWith);
        Assert.Equal(new PolarToken("token-for-two", "polar-2"), second.PulledWith);

        await using var verify = new RideLogDbContext(_options);
        Assert.Equal(
            ["rider-1", "rider-2"],
            await verify.Rides.OrderBy(ride => ride.UserId).Select(ride => ride.UserId).ToListAsync());
    }

    /// <summary>
    /// The daily run is for everyone who has linked, not for whoever the store happened to return
    /// first — that is what it did, so a second rider's rides would never have arrived at all.
    /// </summary>
    [Fact]
    public async Task The_daily_run_covers_every_linked_rider()
    {
        await GivenLinkAsync("rider-1", "token-for-one", "polar-1");
        await GivenLinkAsync("rider-2", "token-for-two", "polar-2");

        var results = await SyncAllAsync(ClientPerRider());

        Assert.Equal(["rider-1", "rider-2"], results.Select(result => result.RiderId).Order());
        Assert.All(results, result => Assert.Equal(1, result.Summary.Imported));
    }

    /// <summary>
    /// An expired token or a Polar outage belongs to the rider it happened to. This codebase has
    /// been here before: the weather top-up threw away a whole batch over one bad ride.
    /// </summary>
    [Fact]
    public async Task One_riders_failure_leaves_the_others_import_intact()
    {
        await GivenLinkAsync("rider-1", "token-for-one", "polar-1");
        await GivenLinkAsync("rider-2", "token-for-two", "polar-2");

        var client = ClientPerRider();
        client.FailsFor("polar-1", new HttpRequestException("401 from Polar"));
        var results = await SyncAllAsync(client);

        var failed = results.Single(result => result.RiderId == "rider-1");
        var survived = results.Single(result => result.RiderId == "rider-2");
        Assert.NotNull(failed.Error);
        Assert.Equal(0, failed.Summary.Imported);
        Assert.Equal(1, survived.Summary.Imported);

        await using var verify = new RideLogDbContext(_options);
        Assert.Equal(["rider-2"], await verify.Rides.Select(ride => ride.UserId).ToListAsync());
    }
}
