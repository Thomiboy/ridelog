using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>Boots the API with a fixed clock so "this month/year" is stable.</summary>
public sealed class FixedClockApiFactory : RideLogApiFactory
{
    /// <summary>The dashboard's "now": 2026-07-17.</summary>
    public static readonly DateTimeOffset Now = new(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider());
    }
}

public class DashboardEndpointTests(FixedClockApiFactory factory) : IClassFixture<FixedClockApiFactory>
{
    private sealed record PeriodDto(double DistanceKm, int RideCount, double ElevationGainMeters);
    private sealed record MonthDto(int Year, int Month, double DistanceKm);
    private sealed record SpeedDto(int Year, int Month, double? AverageSpeedKmh);
    private sealed record DashboardDto(
        PeriodDto ThisMonth, PeriodDto ThisYear, PeriodDto LastYear,
        IReadOnlyList<MonthDto> MonthlyDistance, IReadOnlyList<SpeedDto> AverageSpeedTrend);

    private static Ride Ride(DateTimeOffset start, double km, double elevation, double avgSpeed, string sport = "ROAD_BIKING") => new()
    {
        Id = Guid.NewGuid(),
        UserId = "admin-1",
        StartTime = start,
        EndTime = start.AddHours(2),
        Duration = TimeSpan.FromHours(2),
        DistanceMeters = km * 1000,
        ElevationGainMeters = elevation,
        AverageSpeedKmh = avgSpeed,
        Sport = sport,
        Source = RideSource.Polar,
    };

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(
            Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 60, elevation: 400, avgSpeed: 30),
            Ride(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero), km: 40, elevation: 200, avgSpeed: 32),
            Ride(new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero), km: 100, elevation: 500, avgSpeed: 28),
            Ride(new DateTimeOffset(2025, 7, 20, 8, 0, 0, TimeSpan.Zero), km: 80, elevation: 300, avgSpeed: 25),
            Ride(new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero), km: 10, elevation: 50, avgSpeed: 10, sport: "RUNNING"));
        await context.SaveChangesAsync();
    }

    private sealed record SameMonthDto(int Year, int Month, double DistanceKm, int RideCount);
    private sealed record SameMonthDashboardDto(SameMonthDto SameMonthLastYear);

    [Fact]
    public async Task Same_month_last_year_is_the_matching_month_not_last_years_best()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            context.Rides.AddRange(
                // "Now" is 2026-07-17. May 2025 is last year's *best* month by distance...
                Ride(new DateTimeOffset(2025, 5, 10, 8, 0, 0, TimeSpan.Zero), km: 200, elevation: 900, avgSpeed: 30),
                // ...but July 2025 is the month we are actually in, a year earlier: 80 + 30 = 110 km, 2 rides.
                Ride(new DateTimeOffset(2025, 7, 3, 8, 0, 0, TimeSpan.Zero), km: 80, elevation: 300, avgSpeed: 28),
                Ride(new DateTimeOffset(2025, 7, 28, 8, 0, 0, TimeSpan.Zero), km: 30, elevation: 100, avgSpeed: 26),
                // An adjacent month must not leak into the figure.
                Ride(new DateTimeOffset(2025, 8, 2, 8, 0, 0, TimeSpan.Zero), km: 500, elevation: 100, avgSpeed: 26));
            await context.SaveChangesAsync();
        }

        var dashboard = await factory.CreateClient().GetFromJsonAsync<SameMonthDashboardDto>("/dashboard");

        Assert.Equal(2025, dashboard!.SameMonthLastYear.Year);
        Assert.Equal(7, dashboard.SameMonthLastYear.Month);
        Assert.Equal(110, dashboard.SameMonthLastYear.DistanceKm, 0.01);
        Assert.Equal(2, dashboard.SameMonthLastYear.RideCount);
    }

    private sealed record TempTrendDto(int Year, int Month, double? AverageTemperatureCelsius);
    private sealed record TempTrendDashboardDto(IReadOnlyList<TempTrendDto> AverageTemperatureTrend);

    [Fact]
    public async Task Average_temperature_trend_is_the_monthly_mean_over_the_last_twelve_months()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            await context.SaveChangesAsync();

            var julyA = Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 60, elevation: 400, avgSpeed: 30);
            julyA.AverageTemperatureCelsius = 20;
            var julyB = Ride(new DateTimeOffset(2026, 7, 12, 8, 0, 0, TimeSpan.Zero), km: 40, elevation: 200, avgSpeed: 32);
            julyB.AverageTemperatureCelsius = 24;
            // June ride without a temperature reading: June's trend point stays a gap (null).
            var june = Ride(new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero), km: 50, elevation: 300, avgSpeed: 29);
            context.Rides.AddRange(julyA, julyB, june);
            await context.SaveChangesAsync();
        }

        var dashboard = await factory.CreateClient().GetFromJsonAsync<TempTrendDashboardDto>("/dashboard");

        var trend = dashboard!.AverageTemperatureTrend;
        Assert.Equal(12, trend.Count); // same 12-month window as the speed trend
        Assert.Equal(22, trend.Single(t => t.Year == 2026 && t.Month == 7).AverageTemperatureCelsius!.Value, 0.01); // (20+24)/2
        Assert.Null(trend.Single(t => t.Year == 2026 && t.Month == 6).AverageTemperatureCelsius); // no reading
    }

    [Fact]
    public async Task Aggregates_are_correct_for_seeded_rides_and_public()
    {
        await SeedAsync();

        var response = await factory.CreateClient().GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // anonymous
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>();

        // Hand-computed from the seed (running ride excluded):
        // July 2026: 60 + 40 = 100 km, 2 rides, 600 m. Year 2026: +100 km March = 200 km, 3 rides, 1100 m.
        Assert.Equal(100, dashboard!.ThisMonth.DistanceKm, 0.01);
        Assert.Equal(2, dashboard.ThisMonth.RideCount);
        Assert.Equal(600, dashboard.ThisMonth.ElevationGainMeters, 0.01);

        Assert.Equal(200, dashboard.ThisYear.DistanceKm, 0.01);
        Assert.Equal(3, dashboard.ThisYear.RideCount);
        Assert.Equal(1100, dashboard.ThisYear.ElevationGainMeters, 0.01);

        // Monthly distance covers current + previous year (12 entries each, zeros included).
        Assert.Equal(24, dashboard.MonthlyDistance.Count);
        Assert.Equal(100, dashboard.MonthlyDistance.Single(m => m.Year == 2026 && m.Month == 7).DistanceKm, 0.01);
        Assert.Equal(100, dashboard.MonthlyDistance.Single(m => m.Year == 2026 && m.Month == 3).DistanceKm, 0.01);
        Assert.Equal(80, dashboard.MonthlyDistance.Single(m => m.Year == 2025 && m.Month == 7).DistanceKm, 0.01);
        Assert.Equal(0, dashboard.MonthlyDistance.Single(m => m.Year == 2026 && m.Month == 1).DistanceKm, 0.01);

        // Avg speed trend: last 12 months ending now. July 2026 = (30 + 32) / 2 = 31.
        Assert.Equal(12, dashboard.AverageSpeedTrend.Count);
        Assert.Equal(31, dashboard.AverageSpeedTrend.Single(s => s.Year == 2026 && s.Month == 7).AverageSpeedKmh!.Value, 0.01);
        Assert.Equal(28, dashboard.AverageSpeedTrend.Single(s => s.Year == 2026 && s.Month == 3).AverageSpeedKmh!.Value, 0.01);
        Assert.Null(dashboard.AverageSpeedTrend.Single(s => s.Year == 2026 && s.Month == 1).AverageSpeedKmh);
    }

    [Fact]
    public async Task Last_year_totals_come_from_the_previous_year()
    {
        await SeedAsync();

        var dashboard = await factory.CreateClient().GetFromJsonAsync<DashboardDto>("/dashboard");

        // The only 2025 cycling ride: 2025-07-20, 80 km, 300 m, 1 ride.
        Assert.Equal(80, dashboard!.LastYear.DistanceKm, 0.01);
        Assert.Equal(1, dashboard.LastYear.RideCount);
        Assert.Equal(300, dashboard.LastYear.ElevationGainMeters, 0.01);
    }

    [Fact]
    public async Task Last_year_is_empty_when_the_previous_year_had_no_rides()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            // Current-year rides only — nothing in 2025.
            context.Rides.Add(Ride(new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero), km: 60, elevation: 400, avgSpeed: 30));
            await context.SaveChangesAsync();
        }

        var dashboard = await factory.CreateClient().GetFromJsonAsync<DashboardDto>("/dashboard");

        Assert.Equal(0, dashboard!.LastYear.DistanceKm, 0.01);
        Assert.Equal(0, dashboard.LastYear.RideCount);
    }

    [Fact]
    public async Task Same_month_last_year_reads_zero_rather_than_going_missing()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.RemoveRange(context.Rides);
            // Rides last year, but none in July — the month we are in now.
            context.Rides.Add(Ride(new DateTimeOffset(2025, 4, 8, 8, 0, 0, TimeSpan.Zero), km: 90, elevation: 200, avgSpeed: 28));
            await context.SaveChangesAsync();
        }

        var dashboard = await factory.CreateClient().GetFromJsonAsync<SameMonthDashboardDto>("/dashboard");

        // The month existed, so zero is the answer; the tiles still have something to render.
        Assert.Equal(2025, dashboard!.SameMonthLastYear.Year);
        Assert.Equal(7, dashboard.SameMonthLastYear.Month);
        Assert.Equal(0, dashboard.SameMonthLastYear.DistanceKm, 0.01);
        Assert.Equal(0, dashboard.SameMonthLastYear.RideCount);
    }
}
