using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetDashboardQueryHandler(RideLogDbContext context, TimeProvider clock)
    : IQueryHandler<GetDashboardQuery, DashboardStats>
{
    private sealed record Row(DateTimeOffset StartTime, double DistanceMeters, double? ElevationGainMeters, double? AverageSpeedKmh);

    public async Task<DashboardStats> HandleAsync(GetDashboardQuery query, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var currentYear = now.Year;

        var cycling = context.Rides.AsQueryable();
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        // Lightweight projection; grouping runs in memory (SQLite can't translate DateTimeOffset
        // parts, and at single-user scale two years of summary rows are small).
        var rows = await cycling
            .Select(ride => new Row(ride.StartTime, ride.DistanceMeters, ride.ElevationGainMeters, ride.AverageSpeedKmh))
            .ToListAsync(cancellationToken);

        var relevant = rows.Where(row => row.StartTime.Year >= currentYear - 1).ToList();

        var thisMonth = Period(relevant.Where(r => r.StartTime.Year == now.Year && r.StartTime.Month == now.Month));
        var thisYear = Period(relevant.Where(r => r.StartTime.Year == now.Year));

        var lastYearRides = relevant.Where(r => r.StartTime.Year == currentYear - 1).ToList();
        var lastYear = Period(lastYearRides);
        var lastYearBestMonth = lastYearRides
            .GroupBy(r => r.StartTime.Month)
            .Select(g => new BestMonth(g.Key, Math.Round(g.Sum(r => r.DistanceMeters) / 1000.0, 1), g.Count()))
            // Highest distance wins; ties resolve to the earlier month.
            .OrderByDescending(m => m.DistanceKm).ThenBy(m => m.Month)
            .FirstOrDefault();

        var monthlyDistance = new List<MonthlyDistance>(24);
        foreach (var year in new[] { currentYear - 1, currentYear })
        {
            for (var month = 1; month <= 12; month++)
            {
                var km = relevant
                    .Where(r => r.StartTime.Year == year && r.StartTime.Month == month)
                    .Sum(r => r.DistanceMeters) / 1000.0;
                monthlyDistance.Add(new MonthlyDistance(year, month, Math.Round(km, 1)));
            }
        }

        var speedTrend = new List<MonthlySpeed>(12);
        for (var offset = 11; offset >= 0; offset--)
        {
            var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-offset);
            var speeds = relevant
                .Where(r => r.StartTime.Year == month.Year && r.StartTime.Month == month.Month && r.AverageSpeedKmh.HasValue)
                .Select(r => r.AverageSpeedKmh!.Value)
                .ToList();
            speedTrend.Add(new MonthlySpeed(month.Year, month.Month, speeds.Count > 0 ? Math.Round(speeds.Average(), 1) : null));
        }

        return new DashboardStats(thisMonth, thisYear, lastYear, lastYearBestMonth, monthlyDistance, speedTrend);
    }

    private static PeriodStats Period(IEnumerable<Row> rides)
    {
        var list = rides.ToList();
        return new PeriodStats(
            Math.Round(list.Sum(r => r.DistanceMeters) / 1000.0, 1),
            list.Count,
            list.Sum(r => r.ElevationGainMeters ?? 0));
    }
}
