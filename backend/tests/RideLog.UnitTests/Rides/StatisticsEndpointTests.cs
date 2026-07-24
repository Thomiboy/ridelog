using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// The Statistics page's single feed: all-years monthly aggregates plus records.
/// Boots the API with the same fixed clock as the dashboard so "now" is stable.
/// </summary>
public class StatisticsEndpointTests(FixedClockApiFactory factory) : IClassFixture<FixedClockApiFactory>
{
    private sealed record MonthlyAggregateDto(
        int Year, int Month, double DistanceKm, double ElevationGainMeters, int RideCount, int Calories);

    private sealed record StatisticsDto(IReadOnlyList<MonthlyAggregateDto> MonthlyAggregates);

    private sealed record LongestRideDto(Guid Id, DateTimeOffset Date, double DistanceKm);
    private sealed record FastestAverageDto(Guid Id, DateTimeOffset Date, double AverageSpeedKmh);
    private sealed record StreakDto(int Days, DateOnly StartDate, DateOnly EndDate);
    private sealed record RecordsDto(
        LongestRideDto? LongestRide, FastestAverageDto? FastestAverage, StreakDto? LongestStreak);
    private sealed record RecordsStatisticsDto(RecordsDto Records);

    private static Ride Ride(
        DateTimeOffset start, double km, double elevation, double avgSpeed, int calories, string sport = "ROAD_BIKING") => new()
    {
        Id = Guid.NewGuid(),
        UserId = "admin-1",
        StartTime = start,
        EndTime = start.AddHours(2),
        Duration = TimeSpan.FromHours(2),
        DistanceMeters = km * 1000,
        ElevationGainMeters = elevation,
        AverageSpeedKmh = avgSpeed,
        Calories = calories,
        Sport = sport,
        Source = RideSource.Polar,
    };

    private static Ride TempRide(DateTimeOffset start, double avg, double min, double max)
    {
        var ride = Ride(start, km: 40, elevation: 100, avgSpeed: 30, calories: 500);
        ride.AverageTemperatureCelsius = avg;
        ride.MinTemperatureCelsius = min;
        ride.MaxTemperatureCelsius = max;
        return ride;
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(
            Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 60, elevation: 400, avgSpeed: 30, calories: 800),
            Ride(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero), km: 40, elevation: 200, avgSpeed: 32, calories: 500),
            Ride(new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero), km: 100, elevation: 500, avgSpeed: 28, calories: 1500),
            Ride(new DateTimeOffset(2025, 7, 20, 8, 0, 0, TimeSpan.Zero), km: 80, elevation: 300, avgSpeed: 25, calories: 1000),
            Ride(new DateTimeOffset(2024, 5, 2, 8, 0, 0, TimeSpan.Zero), km: 50, elevation: 250, avgSpeed: 27, calories: 700),
            // Non-cycling: must be excluded everywhere.
            Ride(new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero), km: 10, elevation: 50, avgSpeed: 10, calories: 100, sport: "RUNNING"));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a records scenario and returns the ids we assert against:
    /// <c>longest</c> = the 120 km ride, <c>fastest</c> = the fastest ride of at least 30 km.
    /// </summary>
    private async Task<(Guid Longest, Guid Fastest)> SeedForRecordsAsync()
    {
        var longest = Guid.NewGuid();
        var fastest = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();

        static Ride Explicit(Guid id, DateTimeOffset start, double km, double avgSpeed, string sport = "ROAD_BIKING") => new()
        {
            Id = id,
            UserId = "admin-1",
            StartTime = start,
            EndTime = start.AddHours(2),
            Duration = TimeSpan.FromHours(2),
            DistanceMeters = km * 1000,
            ElevationGainMeters = 100,
            AverageSpeedKmh = avgSpeed,
            Calories = 500,
            Sport = sport,
            Source = RideSource.Polar,
        };

        context.Rides.AddRange(
            // A 3-day streak (Jun 1-3): the longest ride and the short high-speed decoy live here.
            Explicit(longest, new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), km: 120, avgSpeed: 33),
            Explicit(fastest, new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), km: 40, avgSpeed: 35),
            // 20 km ride at 40 km/h: fastest overall but below the 30 km threshold — must not win.
            Explicit(Guid.NewGuid(), new DateTimeOffset(2026, 6, 3, 8, 0, 0, TimeSpan.Zero), km: 20, avgSpeed: 40),
            // Isolated day, well inside the 30 km threshold.
            Explicit(Guid.NewGuid(), new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero), km: 50, avgSpeed: 30),
            // Non-cycling ride the same day must not extend the streak or win any record.
            Explicit(Guid.NewGuid(), new DateTimeOffset(2026, 6, 5, 8, 0, 0, TimeSpan.Zero), km: 200, avgSpeed: 99, sport: "RUNNING"));
        await context.SaveChangesAsync();

        return (longest, fastest);
    }

    [Fact]
    public async Task Longest_ride_record_is_the_greatest_single_ride_distance()
    {
        var (longest, _) = await SeedForRecordsAsync();

        var stats = await factory.CreateClient().GetFromJsonAsync<RecordsStatisticsDto>("/statistics");

        Assert.NotNull(stats!.Records.LongestRide);
        Assert.Equal(longest, stats.Records.LongestRide!.Id);
        Assert.Equal(120, stats.Records.LongestRide.DistanceKm, 0.01);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), stats.Records.LongestRide.Date);
    }

    [Fact]
    public async Task Fastest_average_record_ignores_rides_under_thirty_km()
    {
        var (_, fastest) = await SeedForRecordsAsync();

        var stats = await factory.CreateClient().GetFromJsonAsync<RecordsStatisticsDto>("/statistics");

        Assert.NotNull(stats!.Records.FastestAverage);
        // The 20 km ride at 40 km/h is faster but below 30 km, so the 40 km ride at 35 km/h wins.
        Assert.Equal(fastest, stats.Records.FastestAverage!.Id);
        Assert.Equal(35, stats.Records.FastestAverage.AverageSpeedKmh, 0.01);
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), stats.Records.FastestAverage.Date);
    }

    [Fact]
    public async Task Longest_streak_counts_consecutive_calendar_days_with_a_cycling_ride()
    {
        await SeedForRecordsAsync();

        var stats = await factory.CreateClient().GetFromJsonAsync<RecordsStatisticsDto>("/statistics");

        Assert.NotNull(stats!.Records.LongestStreak);
        // Cycling rides on Jun 1, 2, 3 → a 3-day streak. The Jun 5 running ride does not extend it,
        // and the isolated Jul 10 ride is only a single day.
        Assert.Equal(3, stats.Records.LongestStreak!.Days);
        Assert.Equal(new DateOnly(2026, 6, 1), stats.Records.LongestStreak.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 3), stats.Records.LongestStreak.EndDate);
    }

    private sealed record HrZoneSliceDto(int Zone, double Minutes);
    private sealed record ZonesStatsDto(IReadOnlyList<HrZoneSliceDto>? HrZones);

    [Fact]
    public async Task Aggregates_time_in_hr_zones_across_rides()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            context.UserSettings.RemoveRange(context.UserSettings);
            await context.SaveChangesAsync();

            var rideA = Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 40, elevation: 100, avgSpeed: 30, calories: 500);
            rideA.MetricSeries = [new MetricSample(0, 0, null, 130), new MetricSample(1, 10, null, 150)]; // Z2 owns 10 min
            var rideB = Ride(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero), km: 40, elevation: 100, avgSpeed: 30, calories: 500);
            rideB.MetricSeries = [new MetricSample(0, 0, null, 170), new MetricSample(1, 10, null, 190)]; // Z4 owns 10 min
            context.Rides.AddRange(rideA, rideB);
            context.UserSettings.Add(new RideLog.Domain.Users.UserSettings { UserId = "admin-1", MaxHeartRate = 200 });
            await context.SaveChangesAsync();
        }

        var stats = await factory.CreateClient().GetFromJsonAsync<ZonesStatsDto>("/statistics");

        Assert.NotNull(stats!.HrZones);
        Assert.Equal(5, stats.HrZones!.Count);
        Assert.Equal(10, stats.HrZones.Single(z => z.Zone == 2).Minutes, 0.01);
        Assert.Equal(10, stats.HrZones.Single(z => z.Zone == 4).Minutes, 0.01);
        Assert.Equal(0, stats.HrZones.Single(z => z.Zone == 3).Minutes, 0.01);
    }

    private sealed record BandDto(int? FromCelsius, int? ToCelsius, double Km);
    private sealed record ExtremeDto(Guid Id, DateTimeOffset Date, double AverageTemperatureCelsius);
    private sealed record MonthlyTempDto(int Year, int Month, double AverageTemperatureCelsius);
    private sealed record TempStatsDto(
        IReadOnlyList<BandDto> Distribution, ExtremeDto? Coldest, ExtremeDto? Warmest,
        double? SeasonMinCelsius, double? SeasonMaxCelsius, IReadOnlyList<MonthlyTempDto> MonthlyAverage);
    private sealed record TempResultDto(TempStatsDto? Temperature);

    [Fact]
    public async Task Reports_temperature_extremes_season_range_and_monthly_average()
    {
        Guid coldId, warmId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            await context.SaveChangesAsync();

            var cold = TempRide(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), avg: 5, min: 2, max: 8);
            var mild = TempRide(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero), avg: 18, min: 15, max: 22);
            var warm = TempRide(new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero), avg: 20, min: 18, max: 25);
            coldId = cold.Id;
            warmId = warm.Id;
            context.Rides.AddRange(cold, mild, warm);
            await context.SaveChangesAsync();
        }

        var stats = await factory.CreateClient().GetFromJsonAsync<TempResultDto>("/statistics");
        var temp = stats!.Temperature!;

        Assert.Equal(coldId, temp.Coldest!.Id);
        Assert.Equal(5, temp.Coldest.AverageTemperatureCelsius, 0.01);
        Assert.Equal(warmId, temp.Warmest!.Id);
        Assert.Equal(20, temp.Warmest.AverageTemperatureCelsius, 0.01);

        Assert.Equal(2, temp.SeasonMinCelsius!.Value, 0.01);  // lowest min
        Assert.Equal(25, temp.SeasonMaxCelsius!.Value, 0.01); // highest max

        // July average = (5 + 18) / 2 = 11.5; August = 20.
        Assert.Equal(11.5, temp.MonthlyAverage.Single(m => m.Year == 2026 && m.Month == 7).AverageTemperatureCelsius, 0.01);
        Assert.Equal(20, temp.MonthlyAverage.Single(m => m.Year == 2026 && m.Month == 8).AverageTemperatureCelsius, 0.01);
    }

    [Fact]
    public async Task Aggregates_distance_per_temperature_band_across_rides()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            await context.SaveChangesAsync();

            var rideA = Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 10, elevation: 100, avgSpeed: 30, calories: 500);
            rideA.MetricSeries = [new MetricSample(0, 0, null, null, 3), new MetricSample(10, 30, null, null, 3)]; // 10 km @ 0–5°C
            var rideB = Ride(new DateTimeOffset(2026, 7, 6, 8, 0, 0, TimeSpan.Zero), km: 20, elevation: 100, avgSpeed: 30, calories: 500);
            rideB.MetricSeries = [new MetricSample(0, 0, null, null, 12), new MetricSample(20, 40, null, null, 12)]; // 20 km @ 10–15°C
            context.Rides.AddRange(rideA, rideB);
            await context.SaveChangesAsync();
        }

        var stats = await factory.CreateClient().GetFromJsonAsync<TempResultDto>("/statistics");

        Assert.NotNull(stats!.Temperature);
        var dist = stats.Temperature!.Distribution;
        Assert.Equal(10, dist.Single(b => b.FromCelsius == 0 && b.ToCelsius == 5).Km, 0.01);
        Assert.Equal(20, dist.Single(b => b.FromCelsius == 10 && b.ToCelsius == 15).Km, 0.01);
        Assert.Equal(0, dist.Single(b => b.FromCelsius == 5 && b.ToCelsius == 10).Km, 0.01);
    }

    [Fact]
    public async Task Temperature_stats_are_null_without_temperature_data()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            context.Rides.Add(Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 10, elevation: 100, avgSpeed: 30, calories: 500));
            await context.SaveChangesAsync();
        }

        var stats = await factory.CreateClient().GetFromJsonAsync<TempResultDto>("/statistics");

        Assert.Null(stats!.Temperature);
    }

    [Fact]
    public async Task Monthly_aggregates_cover_every_year_with_data_and_are_public()
    {
        await SeedAsync();

        var response = await factory.CreateClient().GetAsync("/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // anonymous read
        var stats = await response.Content.ReadFromJsonAsync<StatisticsDto>();

        // One entry per (year, month) that actually has cycling rides — the running ride is excluded,
        // so only four months appear across three years.
        Assert.Equal(4, stats!.MonthlyAggregates.Count);

        var july2026 = stats.MonthlyAggregates.Single(m => m.Year == 2026 && m.Month == 7);
        Assert.Equal(100, july2026.DistanceKm, 0.01);     // 60 + 40
        Assert.Equal(600, july2026.ElevationGainMeters, 0.01); // 400 + 200
        Assert.Equal(2, july2026.RideCount);
        Assert.Equal(1300, july2026.Calories);            // 800 + 500

        var march2026 = stats.MonthlyAggregates.Single(m => m.Year == 2026 && m.Month == 3);
        Assert.Equal(100, march2026.DistanceKm, 0.01);
        Assert.Equal(1, march2026.RideCount);
        Assert.Equal(1500, march2026.Calories);

        Assert.Equal(80, stats.MonthlyAggregates.Single(m => m.Year == 2025 && m.Month == 7).DistanceKm, 0.01);
        Assert.Equal(50, stats.MonthlyAggregates.Single(m => m.Year == 2024 && m.Month == 5).DistanceKm, 0.01);

        // Months without rides are simply absent (the frontend fills the 12-month grid itself).
        Assert.DoesNotContain(stats.MonthlyAggregates, m => m.Year == 2026 && m.Month == 1);
    }
}
