using RideLog.Application.Rides;

namespace RideLog.Infrastructure.Rides;

/// <summary>The fields a month's totals are built from; the rest of a ride is none of their business.</summary>
internal sealed record MonthlyRideFacts(
    DateTimeOffset StartTime,
    double DistanceMeters,
    TimeSpan Duration,
    double? ElevationGainMeters,
    int? Calories);

/// <summary>
/// One calendar month's cycling totals — the grain the Trends charts are drawn on, and the grain a
/// monthly analysis is written at. It lives here rather than inside either reader so both get the
/// same numbers from the same place: an analysis that disagreed with the chart above it would be
/// worse than no analysis (docs/adr/0003 on two paths to one number).
/// </summary>
internal static class MonthlyAggregates
{
    public static List<MonthlyAggregate> Group(IEnumerable<MonthlyRideFacts> rides) => rides
        .GroupBy(r => (r.StartTime.Year, r.StartTime.Month))
        .Select(g => new MonthlyAggregate(
            g.Key.Year,
            g.Key.Month,
            Math.Round(g.Sum(r => r.DistanceMeters) / 1000.0, 1),
            g.Sum(r => r.ElevationGainMeters ?? 0),
            g.Count(),
            g.Sum(r => r.Calories ?? 0),
            // Moving time (docs/adr/0001), in minutes — the finest unit; the chart converts to hours.
            Math.Round(g.Sum(r => r.Duration.TotalMinutes), 1)))
        .OrderBy(m => m.Year).ThenBy(m => m.Month)
        .ToList();

    /// <summary>A month nobody rode: present, and honestly zero, rather than missing.</summary>
    public static MonthlyAggregate Empty(int year, int month) => new(year, month, 0, 0, 0, 0, 0);
}
