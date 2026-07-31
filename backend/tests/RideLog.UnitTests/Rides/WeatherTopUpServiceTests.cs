using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Routes;
using RideLog.Application.Weather;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Weather;

namespace RideLog.UnitTests.Rides;

public sealed class WeatherTopUpServiceTests : IDisposable
{
    private const string UserId = "user-1";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public WeatherTopUpServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Stores_what_the_provider_returns_and_marks_the_ride_fetched()
    {
        var rideId = await GivenRide(start: new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero));
        var provider = new StubProvider(WeatherLookup.Fetched([
            Reading(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), windSpeedKmh: 12),
            Reading(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), windSpeedKmh: 18),
        ]));

        var summary = await TopUp(provider);

        Assert.Equal(new WeatherTopUpSummary(Fetched: 1, Unavailable: 0, Failed: 0), summary);

        var stored = await StoredRide(rideId);
        Assert.Equal(WeatherOutcome.Fetched, stored.WeatherOutcome);
        Assert.NotNull(stored.Weather);
        Assert.Equal([12, 18], stored.Weather!.Select(reading => reading.WindSpeedKmh));
    }

    // The daily top-up runs against the whole archive, so what it *skips* is what keeps it bounded.
    // A ride the service already has weather for needs nothing; one the service will never have data
    // for (a date before the archive begins) would otherwise be asked about every single morning,
    // against the quota, forever. A failure is the only absence worth another try.
    [Fact]
    public async Task Asks_only_about_rides_never_tried_and_rides_that_failed()
    {
        var untried = await GivenRide(Hour(8), outcome: null);
        var alreadyFetched = await GivenRide(Hour(10), outcome: WeatherOutcome.Fetched);
        var hopeless = await GivenRide(Hour(12), outcome: WeatherOutcome.Unavailable);
        var worthRetrying = await GivenRide(Hour(14), outcome: WeatherOutcome.Failed);

        var provider = new StubProvider(WeatherLookup.Fetched([Reading(Hour(8), windSpeedKmh: 5)]));
        await TopUp(provider);

        Assert.Equal([Hour(14), Hour(8)], provider.AskedAbout);
        Assert.Equal(WeatherOutcome.Fetched, (await StoredRide(untried)).WeatherOutcome);
        Assert.Equal(WeatherOutcome.Fetched, (await StoredRide(worthRetrying)).WeatherOutcome);
        Assert.Null((await StoredRide(alreadyFetched)).Weather);
        Assert.Equal(WeatherOutcome.Unavailable, (await StoredRide(hopeless)).WeatherOutcome);
    }

    private static DateTimeOffset Hour(int hour) => new(2026, 6, 1, hour, 0, 0, TimeSpan.Zero);

    private static WeatherReading Reading(DateTimeOffset hour, double windSpeedKmh) =>
        new(hour, TemperatureCelsius: null, windSpeedKmh, WindFromBearing: null,
            PrecipitationMm: null, RelativeHumidityPercent: null, CloudCoverPercent: null, WeatherCode: null);

    private async Task<Guid> GivenRide(DateTimeOffset start, WeatherOutcome? outcome = null)
    {
        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            StartTime = start,
            EndTime = start.AddHours(2),
            DistanceMeters = 30_000,
            Duration = TimeSpan.FromHours(2),
            Sport = "Biking",
            Source = RideSource.Import,
            RoutePolyline = PolylineEncoder.Encode([new GeoPoint(47.50, 19.04), new GeoPoint(47.55, 19.10)]),
            WeatherOutcome = outcome,
        };

        await using var context = new RideLogDbContext(_options);
        context.Rides.Add(ride);
        await context.SaveChangesAsync();
        return ride.Id;
    }

    private async Task<WeatherTopUpSummary> TopUp(IWeatherProvider provider)
    {
        await using var context = new RideLogDbContext(_options);
        var service = new WeatherTopUpService(context, provider);
        return await service.TopUpAsync(UserId, max: 10);
    }

    private async Task<Ride> StoredRide(Guid rideId)
    {
        await using var context = new RideLogDbContext(_options);
        return await context.Rides.SingleAsync(ride => ride.Id == rideId);
    }

    private sealed class StubProvider(WeatherLookup result) : IWeatherProvider
    {
        private readonly List<DateTimeOffset> _askedAbout = [];

        /// <summary>Ride start times the service actually looked up, in the order it asked.</summary>
        public IReadOnlyList<DateTimeOffset> AskedAbout => _askedAbout;

        public Task<WeatherLookup> GetHourlyAsync(
            double latitude, double longitude, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            _askedAbout.Add(from);
            return Task.FromResult(result);
        }
    }
}
