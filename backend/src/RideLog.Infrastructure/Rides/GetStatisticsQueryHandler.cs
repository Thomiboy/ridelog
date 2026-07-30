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
        Guid Id, string UserId, DateTimeOffset StartTime, double DistanceMeters, TimeSpan Duration,
        double? ElevationGainMeters, int? Calories, double? AverageSpeedKmh, double? MaximumSpeedKmh,
        IReadOnlyList<MetricSample>? MetricSeries,
        double? AverageTemperatureCelsius, double? MinTemperatureCelsius, double? MaxTemperatureCelsius);

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
                ride.Id, ride.UserId, ride.StartTime, ride.DistanceMeters, ride.Duration,
                ride.ElevationGainMeters, ride.Calories, ride.AverageSpeedKmh, ride.MaximumSpeedKmh, ride.MetricSeries,
                ride.AverageTemperatureCelsius, ride.MinTemperatureCelsius, ride.MaxTemperatureCelsius))
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
                g.Sum(r => r.Calories ?? 0),
                // Moving time (docs/adr/0001), in minutes — the finest unit; the chart converts to hours.
                Math.Round(g.Sum(r => r.Duration.TotalMinutes), 1)))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new StatisticsResult(
            monthlyAggregates, BuildRecords(rows), AggregateHrZones(rows, maxHeartRateByUser), AggregateTemperature(rows));
    }

    private static TemperatureStats? AggregateTemperature(IReadOnlyList<Row> rows)
    {
        // Distance-per-band comes from the per-point series; extremes and trend from the per-ride summary.
        var bandCount = TemperatureBandCalculator.Bands.Count;
        var totals = new double[bandCount];
        var perYear = new Dictionary<int, double[]>();
        var hasSeriesTemperature = false;
        foreach (var row in rows)
        {
            if (row.MetricSeries is { } series && series.Any(s => s.TemperatureCelsius is not null))
            {
                hasSeriesTemperature = true;
                var bands = TemperatureBandCalculator.KmPerBand(series);
                var yearTotals = perYear.TryGetValue(row.StartTime.Year, out var existing)
                    ? existing
                    : perYear[row.StartTime.Year] = new double[bandCount];
                for (var i = 0; i < bands.Count; i++)
                {
                    totals[i] += bands[i].Km;
                    yearTotals[i] += bands[i].Km;
                }
            }
        }

        var withAverage = rows.Where(r => r.AverageTemperatureCelsius is not null).ToList();
        if (!hasSeriesTemperature && withAverage.Count == 0)
        {
            return null;
        }

        var distribution = TemperatureBandCalculator.Bands
            .Select((band, i) => new TemperatureBandSlice(band.From, band.To, Math.Round(totals[i], 1)))
            .ToList();

        // Every band per year (including empty ones) so the client renders a stable per-year chart.
        var yearlyDistribution = perYear
            .OrderBy(entry => entry.Key)
            .SelectMany(entry => TemperatureBandCalculator.Bands
                .Select((band, i) => new YearlyTemperatureBand(entry.Key, band.From, band.To, Math.Round(entry.Value[i], 1))))
            .ToList();

        var coldest = withAverage
            .OrderBy(r => r.AverageTemperatureCelsius).ThenBy(r => r.StartTime)
            .Select(r => new TemperatureExtreme(r.Id, r.StartTime, r.AverageTemperatureCelsius!.Value))
            .FirstOrDefault();
        var warmest = withAverage
            .OrderByDescending(r => r.AverageTemperatureCelsius).ThenBy(r => r.StartTime)
            .Select(r => new TemperatureExtreme(r.Id, r.StartTime, r.AverageTemperatureCelsius!.Value))
            .FirstOrDefault();

        // Nullable Min/Max return null for an empty sequence, so absent readings give a null range.
        var seasonMin = rows.Select(r => r.MinTemperatureCelsius).Where(t => t is not null).Min();
        var seasonMax = rows.Select(r => r.MaxTemperatureCelsius).Where(t => t is not null).Max();

        var monthlyAverage = withAverage
            .GroupBy(r => (r.StartTime.Year, r.StartTime.Month))
            .Select(g => new MonthlyTemperature(g.Key.Year, g.Key.Month, Math.Round(g.Average(r => r.AverageTemperatureCelsius!.Value), 1)))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return new TemperatureStats(distribution, coldest, warmest, seasonMin, seasonMax, monthlyAverage, yearlyDistribution);
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

        // Top speed: the highest a ride ever reached; rides that recorded none can't win. Ties → earlier.
        var maxSpeed = rows
            .Where(r => r.MaximumSpeedKmh.HasValue)
            .OrderByDescending(r => r.MaximumSpeedKmh).ThenBy(r => r.StartTime)
            .Select(r => new MaxSpeedRecord(r.Id, r.StartTime, Math.Round(r.MaximumSpeedKmh!.Value, 1)))
            .FirstOrDefault();

        // Biggest climb: greatest elevation gain in one ride; rides without a reading can't win. Ties → earlier.
        var biggestClimb = rows
            .Where(r => r.ElevationGainMeters.HasValue)
            .OrderByDescending(r => r.ElevationGainMeters).ThenBy(r => r.StartTime)
            .Select(r => new BiggestClimbRecord(r.Id, r.StartTime, Math.Round(r.ElevationGainMeters!.Value)))
            .FirstOrDefault();

        // Fastest average speed, but only among rides long enough not to skew the record.
        var fastest = rows
            .Where(r => r.AverageSpeedKmh.HasValue
                && r.DistanceMeters / 1000.0 >= StatisticsRecords.FastestAverageMinimumKm)
            .OrderByDescending(r => r.AverageSpeedKmh).ThenBy(r => r.StartTime)
            .Select(r => new FastestAverageRecord(r.Id, r.StartTime, Math.Round(r.AverageSpeedKmh!.Value, 1)))
            .FirstOrDefault();

        // Most calories: greatest single-ride burn; rides without a reading are ignored; ties → earlier.
        var mostCalories = rows
            .Where(r => r.Calories is > 0)
            .OrderByDescending(r => r.Calories).ThenBy(r => r.StartTime)
            .Select(r => new MostCaloriesRecord(r.Id, r.StartTime, r.Calories!.Value))
            .FirstOrDefault();

        // Longest duration: greatest moving time (docs/adr/0001 — the device's timer where the source
        // has one, elapsed otherwise); zero-duration rides are ignored; ties → earlier.
        var longestDuration = rows
            .Where(r => r.Duration > TimeSpan.Zero)
            .OrderByDescending(r => r.Duration).ThenBy(r => r.StartTime)
            .Select(r => new LongestDurationRecord(r.Id, r.StartTime, Math.Round(r.Duration.TotalMinutes)))
            .FirstOrDefault();

        // Best calendar month by distance and by ride count. The month in progress competes like any
        // other; ties resolve to the more recent month, matching the streak record.
        var months = rows
            .GroupBy(r => (r.StartTime.Year, r.StartTime.Month))
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                DistanceKm = Math.Round(g.Sum(r => r.DistanceMeters) / 1000.0, 1),
                RideCount = g.Count(),
            })
            .ToList();

        var bestMonthDistance = months
            .OrderByDescending(m => m.DistanceKm).ThenByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .Select(m => new BestMonthDistanceRecord(m.Year, m.Month, m.DistanceKm))
            .FirstOrDefault();

        var bestMonthRides = months
            .OrderByDescending(m => m.RideCount).ThenByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .Select(m => new BestMonthRidesRecord(m.Year, m.Month, m.RideCount))
            .FirstOrDefault();

        return new StatisticsRecords(
            longest, fastest, LongestStreak(rows), mostCalories, longestDuration, bestMonthDistance, bestMonthRides,
            maxSpeed, biggestClimb);
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

        // Collect every run, then pick a winner: longest wins, and among equally long ones the one
        // covering more distance, with the more recent breaking any remaining tie.
        var runs = new List<(DateOnly Start, DateOnly End)>();
        var runStart = days[0];

        for (var i = 1; i <= days.Count; i++)
        {
            if (i < days.Count && days[i] == days[i - 1].AddDays(1))
            {
                continue;
            }

            runs.Add((runStart, days[i - 1]));
            if (i < days.Count)
            {
                runStart = days[i];
            }
        }

        var best = runs
            .Select(run => new
            {
                run.Start,
                run.End,
                Days = run.End.DayNumber - run.Start.DayNumber + 1,
                DistanceKm = DistanceInRange(rows, run.Start, run.End),
            })
            .OrderByDescending(run => run.Days)
            .ThenByDescending(run => run.DistanceKm)
            .ThenByDescending(run => run.End)
            .First();

        return new StreakRecord(best.Days, best.Start, best.End, best.DistanceKm);
    }

    /// <summary>Total ridden distance over the inclusive day range, in km.</summary>
    private static double DistanceInRange(IReadOnlyList<Row> rows, DateOnly start, DateOnly end) =>
        Math.Round(
            rows
                .Where(r => DateOnly.FromDateTime(r.StartTime.Date) >= start && DateOnly.FromDateTime(r.StartTime.Date) <= end)
                .Sum(r => r.DistanceMeters) / 1000.0,
            1);
}
