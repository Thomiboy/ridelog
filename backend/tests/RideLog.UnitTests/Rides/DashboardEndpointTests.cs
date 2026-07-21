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
    private sealed record BestMonthDto(int Month, double DistanceKm, int RideCount);
    private sealed record DashboardDto(
        PeriodDto ThisMonth, PeriodDto ThisYear, PeriodDto LastYear, BestMonthDto? LastYearBestMonth,
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
    public async Task Last_year_totals_and_best_month_come_from_the_previous_year()
    {
        await SeedAsync();

        var dashboard = await factory.CreateClient().GetFromJsonAsync<DashboardDto>("/dashboard");

        // The only 2025 cycling ride: 2025-07-20, 80 km, 300 m, 1 ride.
        Assert.Equal(80, dashboard!.LastYear.DistanceKm, 0.01);
        Assert.Equal(1, dashboard.LastYear.RideCount);
        Assert.Equal(300, dashboard.LastYear.ElevationGainMeters, 0.01);

        Assert.NotNull(dashboard.LastYearBestMonth);
        Assert.Equal(7, dashboard.LastYearBestMonth!.Month); // July
        Assert.Equal(80, dashboard.LastYearBestMonth.DistanceKm, 0.01);
        Assert.Equal(1, dashboard.LastYearBestMonth.RideCount);
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
        Assert.Null(dashboard.LastYearBestMonth);
    }
}
