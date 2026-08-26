using Microsoft.Extensions.DependencyInjection;
using RideLog.Application.Analysis;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Domain.Users;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// The value a monthly analysis is written from, and the only thing that leaves the app (#187).
/// The figures here are worked out by hand from the seed below rather than read back from the code,
/// so the test can disagree with the implementation instead of agreeing with it by construction.
/// </summary>
public class MonthlyTrainingSummaryTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private const string Rider = "admin-1";

    private static Ride Ride(
        DateTimeOffset start, double km, double elevation, int calories, double hours = 2,
        string sport = "ROAD_BIKING", string userId = Rider) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        EndTime = start.AddHours(hours),
        Duration = TimeSpan.FromHours(hours),
        DistanceMeters = km * 1000,
        ElevationGainMeters = elevation,
        Calories = calories,
        Sport = sport,
        Source = RideSource.Polar,
    };

    private static DateTimeOffset On(int year, int month, int day) => new(year, month, day, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The 60 km ride, given the things a coach actually reads: effort and conditions.</summary>
    private static Ride DetailedRide()
    {
        var start = On(2026, 7, 5);
        var ride = Ride(start, km: 60, elevation: 400, calories: 800);
        ride.AverageHeartRate = 142;
        ride.MaximumHeartRate = 171;
        ride.AverageTemperatureCelsius = 24.5;
        // Four points: three intervals, the last sample only closing the one before it. With a max of
        // 180 the floors are 90/108/126/144/162, so 100 is Z1, 130 is Z3 and 150 is Z4.
        ride.MetricSeries =
        [
            new MetricSample(0, 0, ElevationMeters: 100, HeartRate: 100, TemperatureCelsius: 18),
            new MetricSample(10, 30, ElevationMeters: 200, HeartRate: 130, TemperatureCelsius: 22),
            new MetricSample(20, 60, ElevationMeters: 300, HeartRate: 150, TemperatureCelsius: 26),
            new MetricSample(40, 120, ElevationMeters: 400, HeartRate: 170, TemperatureCelsius: 27),
        ];
        ride.Weather =
        [
            new WeatherReading(start, 23.0, WindSpeedKmh: 10, WindFromBearing: 90, PrecipitationMm: 0.4, null, null, null),
            new WeatherReading(start.AddHours(1), 26.0, WindSpeedKmh: 14, WindFromBearing: 95, PrecipitationMm: 0.2, null, null, null),
        ];
        return ride;
    }

    /// <summary>
    /// July 2026 holds two rides — 60 km and 40 km — plus a run that is not cycling. March 2026 falls
    /// inside the twelve months before it; July 2025 sits one month past the far edge of that window.
    /// </summary>
    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.UserSettings.RemoveRange(context.UserSettings);
        context.UserSettings.Add(new UserSettings { UserId = Rider, MaxHeartRate = 180 });
        context.Rides.AddRange(
            DetailedRide(),
            Ride(On(2026, 7, 12), km: 40, elevation: 200, calories: 500),
            Ride(On(2026, 7, 14), km: 10, elevation: 50, calories: 100, sport: "RUNNING"),
            Ride(On(2026, 3, 10), km: 100, elevation: 500, calories: 1500),
            Ride(On(2025, 7, 20), km: 80, elevation: 300, calories: 1000));
        await context.SaveChangesAsync();
    }

    private async Task<MonthlyTrainingSummary> SummaryAsync(int year, int month, string rider = Rider)
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        return await dispatcher.QueryAsync(new GetMonthlyTrainingSummaryQuery(rider, year, month));
    }

    [Fact]
    public async Task The_month_totals_only_the_cycling_that_happened_in_it()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        Assert.Equal(2026, summary.Month.Year);
        Assert.Equal(7, summary.Month.Month);
        Assert.Equal(100.0, summary.Month.DistanceKm);
        Assert.Equal(600, summary.Month.ElevationGainMeters);
        Assert.Equal(2, summary.Month.RideCount);
        Assert.Equal(1300, summary.Month.Calories);
        Assert.Equal(240.0, summary.Month.DurationMinutes);
    }

    /// <summary>
    /// A month is only worth reading against the year that led to it, so the twelve before it travel
    /// with it — and only those. The window ends with the month before this one, which puts July 2025
    /// just outside it: exactly a year back is the thirteenth month, and the boundary is the whole
    /// point of the assertion.
    /// </summary>
    [Fact]
    public async Task The_twelve_months_before_it_travel_with_it_and_no_more()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        Assert.Equal([(2026, 3)], summary.PrecedingMonths.Select(m => (m.Year, m.Month)));
        Assert.Equal(100.0, summary.PrecedingMonths.Single().DistanceKm);
    }

    /// <summary>A month nobody rode still has an answer, and it is zero rather than an error.</summary>
    [Fact]
    public async Task A_month_with_no_cycling_totals_zero()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 6);

        Assert.Equal(0, summary.Month.RideCount);
        Assert.Equal(0.0, summary.Month.DistanceKm);
    }

    /// <summary>
    /// The rides themselves, because a month's totals cannot say what it was made of — eleven short
    /// evenings and one long Sunday total the same as twelve middling ones. Wind and rain are summed
    /// from the hours the ride stored; headwind is not here, because resolving it needs the route and
    /// the per-point series, and neither leaves.
    /// </summary>
    [Fact]
    public async Task Each_ride_of_the_month_travels_as_a_row()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        Assert.Equal(
            [new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 12)],
            summary.Rides.Select(r => r.Date));

        var first = summary.Rides[0];
        Assert.Equal(60.0, first.DistanceKm);
        Assert.Equal(120.0, first.DurationMinutes);
        Assert.Equal(142, first.AverageHeartRate);
        Assert.Equal(171, first.MaximumHeartRate);
        Assert.Equal(400, first.ElevationGainMeters);
        Assert.Equal(24.5, first.AverageTemperatureCelsius);
        Assert.Equal(12.0, first.WindSpeedKmh);
        Assert.Equal(0.6, first.PrecipitationMm!.Value, precision: 5);

        // The 40 km ride stored no weather at all; absent is not the same as calm.
        Assert.Null(summary.Rides[1].WindSpeedKmh);
        Assert.Null(summary.Rides[1].PrecipitationMm);
    }

    /// <summary>
    /// What the per-point series is worth to an analysis: the zone split, not the points. Thirty
    /// minutes at 100 bpm, thirty at 130 and sixty at 150 against a maximum of 180 — the closing
    /// sample credits nothing, because a zone is time between two readings.
    /// </summary>
    [Fact]
    public async Task The_months_effort_travels_as_a_zone_split_not_as_points()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        Assert.Equal(
            [(1, 30.0), (2, 0.0), (3, 30.0), (4, 60.0), (5, 0.0)],
            summary.HrZones!.Select(z => (z.Zone, z.Minutes)));
    }

    /// <summary>
    /// The same for temperature: distance per 5°C band, not the readings. The seeded series rides
    /// 10 km starting at 18°, 10 km starting at 22° and 20 km starting at 26°, so the three bands
    /// that touch it carry 10, 10 and 20 km and every other band is empty.
    /// </summary>
    [Fact]
    public async Task The_months_conditions_travel_as_distance_per_band()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        Assert.Equal(
            [(15, 20, 10.0), (20, 25, 10.0), (25, null, 20.0)],
            summary.TemperatureBands!.Where(b => b.Km > 0).Select(b => (b.FromCelsius, b.ToCelsius, b.Km)));
    }

    /// <summary>
    /// The analysis sits under the Trends charts on the same page, so its month has to be the month
    /// those charts drew. This guards the seam rather than the arithmetic: both readers total a month
    /// in one shared place, and the day someone gives one of them its own copy, this is what says so.
    /// A reading that contradicted the chart above it would be worse than no reading (docs/adr/0003).
    /// </summary>
    [Fact]
    public async Task The_months_figures_are_the_ones_the_statistics_page_shows()
    {
        await SeedAsync();

        var summary = await SummaryAsync(2026, 7);

        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        var statistics = await dispatcher.QueryAsync(new GetStatisticsQuery(Rider));
        var sameMonth = statistics.MonthlyAggregates.Single(m => m is { Year: 2026, Month: 7 });

        Assert.Equal(sameMonth, summary.Month);
    }
}
