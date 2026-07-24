using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetStatisticsQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetStatisticsQuery, StatisticsResult>
{
    private sealed record Row(
        Guid Id, string UserId, DateTimeOffset StartTime, double DistanceMeters,
        double? ElevationGainMeters, int? Calories, double? AverageSpeedKmh,
        IReadOnlyList<MetricSample>? MetricSeries);

    public async Task<StatisticsResult> HandleAsync(GetStatisticsQuery query, CancellationToken cancellationToken = default)
    {
        var cycling = context.Rides.AsQueryable();
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        // Lightweight projection; grouping runs in memory (SQLite can't translate DateTimeOffset parts,
        // and at single-user scale the whole history is a handful of summary rows).
        var rows = await cycling
            .Select(ride => new Row(
                ride.Id, ride.UserId, ride.StartTime, ride.DistanceMeters,
                ride.ElevationGainMeters, ride.Calories, ride.AverageSpeedKmh, ride.MetricSeries))
            .ToListAsync(cancellationToken);

        var maxHeartRateByUser = await context.UserSettings
            .ToDictionaryAsync(s => s.UserId, s => s.MaxHeartRate, cancellationToken);

        var monthlyAggregates = rows
            .GroupBy(r => (r.StartTime.Year, r.StartTime.Month))
            .Select(g => new MonthlyAggregate(
                g.Key.Year,
                g.Key.Month,
                Math.Round(g.Sum(r => r.DistanceMeters) / 1000.0, 1),
                g.Sum(r => r.ElevationGainMeters ?? 0),
                g.Count(),
                g.Sum(r => r.Calories ?? 0)))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new StatisticsResult(monthlyAggregates, BuildRecords(rows), AggregateHrZones(rows, maxHeartRateByUser));
    }

    private static IReadOnlyList<HrZoneSlice>? AggregateHrZones(
        IReadOnlyList<Row> rows, IReadOnlyDictionary<string, int?> maxHeartRateByUser)
    {
        var totals = new double[HrZoneCalculator.ZoneCount];
        foreach (var row in rows)
        {
            if (row.MetricSeries is { } series
                && maxHeartRateByUser.TryGetValue(row.UserId, out var configured)
                && configured is { } maxHeartRate)
            {
                foreach (var slice in HrZoneCalculator.TimeInZone(series, maxHeartRate))
                {
                    totals[slice.Zone - 1] += slice.Minutes;
                }
            }
        }

        return totals.Any(minutes => minutes > 0)
            ? Enumerable.Range(1, HrZoneCalculator.ZoneCount).Select(zone => new HrZoneSlice(zone, totals[zone - 1])).ToList()
            : null;
    }

    private static StatisticsRecords BuildRecords(IReadOnlyList<Row> rows)
    {
        // Longest ride: greatest single-ride distance; ties resolve to the earlier ride.
        var longest = rows
            .OrderByDescending(r => r.DistanceMeters).ThenBy(r => r.StartTime)
            .Select(r => new LongestRideRecord(r.Id, r.StartTime, Math.Round(r.DistanceMeters / 1000.0, 1)))
            .FirstOrDefault();

        // Fastest average speed, but only among rides long enough not to skew the record.
        var fastest = rows
            .Where(r => r.AverageSpeedKmh.HasValue
                && r.DistanceMeters / 1000.0 >= StatisticsRecords.FastestAverageMinimumKm)
            .OrderByDescending(r => r.AverageSpeedKmh).ThenBy(r => r.StartTime)
            .Select(r => new FastestAverageRecord(r.Id, r.StartTime, Math.Round(r.AverageSpeedKmh!.Value, 1)))
            .FirstOrDefault();

        return new StatisticsRecords(longest, fastest, LongestStreak(rows));
    }

    private static StreakRecord? LongestStreak(IReadOnlyList<Row> rows)
    {
        // One entry per calendar day that had a ride; multiple rides on a day count once.
        var days = rows
            .Select(r => DateOnly.FromDateTime(r.StartTime.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (days.Count == 0)
        {
            return null;
        }

        var bestStart = days[0];
        var bestEnd = days[0];
        var runStart = days[0];

        for (var i = 1; i <= days.Count; i++)
        {
            var continues = i < days.Count && days[i] == days[i - 1].AddDays(1);
            if (continues)
            {
                continue;
            }

            var runEnd = days[i - 1];
            if (runEnd.DayNumber - runStart.DayNumber > bestEnd.DayNumber - bestStart.DayNumber)
            {
                bestStart = runStart;
                bestEnd = runEnd;
            }

            if (i < days.Count)
            {
                runStart = days[i];
            }
        }

        return new StreakRecord(bestEnd.DayNumber - bestStart.DayNumber + 1, bestStart, bestEnd);
    }
}
