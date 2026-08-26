using Microsoft.EntityFrameworkCore;
using RideLog.Application.Analysis;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Rides;

namespace RideLog.Infrastructure.Analysis;

/// <summary>
/// Gathers one rider's month into the value an analysis is written from (#187). Every figure comes
/// from the same place the Statistics page reads — nothing is worked out a second way here.
/// </summary>
internal sealed class GetMonthlyTrainingSummaryQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetMonthlyTrainingSummaryQuery, MonthlyTrainingSummary>
{
    private sealed record Row(
        DateTimeOffset StartTime, double DistanceMeters, TimeSpan Duration,
        double? ElevationGainMeters, int? Calories,
        int? AverageHeartRate, int? MaximumHeartRate, double? AverageTemperatureCelsius,
        IReadOnlyList<WeatherReading>? Weather,
        IReadOnlyList<MetricSample>? MetricSeries);

    public async Task<MonthlyTrainingSummary> HandleAsync(
        GetMonthlyTrainingSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var cycling = context.Rides.Where(ride => ride.UserId == query.RiderId);
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        var rows = await cycling
            .Select(ride => new Row(
                ride.StartTime, ride.DistanceMeters, ride.Duration, ride.ElevationGainMeters, ride.Calories,
                ride.AverageHeartRate, ride.MaximumHeartRate, ride.AverageTemperatureCelsius, ride.Weather,
                ride.MetricSeries))
            .ToListAsync(cancellationToken);

        // This rider's own maximum, not anyone else's: the rows are theirs (docs/adr/0006).
        var maxHeartRate = await context.UserSettings
            .Where(settings => settings.UserId == query.RiderId)
            .Select(settings => settings.MaxHeartRate)
            .FirstOrDefaultAsync(cancellationToken);

        var months = MonthlyAggregates.Group(rows.Select(r =>
            new MonthlyRideFacts(r.StartTime, r.DistanceMeters, r.Duration, r.ElevationGainMeters, r.Calories)));

        var month = months.FirstOrDefault(m => m.Year == query.Year && m.Month == query.Month)
            ?? MonthlyAggregates.Empty(query.Year, query.Month);

        // "The twelve months before it" is a window on the calendar, not the last twelve months that
        // happened to have rides — a rider who took a winter off would otherwise reach back years.
        // The window ends with the month before this one, so exactly a year back falls outside it.
        var target = new DateOnly(query.Year, query.Month, 1);
        var first = target.AddMonths(1 - MonthlyTrainingSummary.PrecedingMonthCount);
        var preceding = months
            .Where(m => new DateOnly(m.Year, m.Month, 1) is var start && start >= first && start < target)
            .ToList();

        var inMonth = rows
            .Where(r => r.StartTime.Year == query.Year && r.StartTime.Month == query.Month)
            .OrderBy(r => r.StartTime)
            .ToList();

        return new MonthlyTrainingSummary(
            month,
            preceding,
            inMonth.Select(RowToRide).ToList(),
            ZoneSplit(inMonth, maxHeartRate),
            TemperatureBands(inMonth));
    }

    /// <summary>
    /// The month's time-in-zone, summed over its rides. Null when the rider has set no maximum: the
    /// zones would then rest on a number nobody chose, and a reading built on that is worse than the
    /// absence of one.
    /// </summary>
    private static IReadOnlyList<HrZoneSlice>? ZoneSplit(IReadOnlyList<Row> rows, int? maxHeartRate)
    {
        if (maxHeartRate is not { } maximum)
        {
            return null;
        }

        var minutes = new double[HrZoneCalculator.ZoneCount];
        foreach (var row in rows.Where(r => r.MetricSeries is not null))
        {
            foreach (var slice in HrZoneCalculator.TimeInZone(row.MetricSeries!, maximum))
            {
                minutes[slice.Zone - 1] += slice.Minutes;
            }
        }

        return minutes.Any(m => m > 0)
            ? Enumerable.Range(1, HrZoneCalculator.ZoneCount)
                .Select(zone => new HrZoneSlice(zone, minutes[zone - 1])).ToList()
            : null;
    }

    /// <summary>The month's distance per 5°C band; null when no ride carried a temperature series.</summary>
    private static IReadOnlyList<TemperatureBandSlice>? TemperatureBands(IReadOnlyList<Row> rows)
    {
        var km = new double[TemperatureBandCalculator.Bands.Count];
        var any = false;
        foreach (var row in rows)
        {
            if (row.MetricSeries is not { } series || !series.Any(s => s.TemperatureCelsius is not null))
            {
                continue;
            }

            any = true;
            var bands = TemperatureBandCalculator.KmPerBand(series);
            for (var i = 0; i < bands.Count; i++)
            {
                km[i] += bands[i].Km;
            }
        }

        return any
            ? TemperatureBandCalculator.Bands
                .Select((band, i) => new TemperatureBandSlice(band.From, band.To, Math.Round(km[i], 1))).ToList()
            : null;
    }

    /// <summary>
    /// One ride, stripped to what may leave. Wind and rain are the ride's own stored hours summed —
    /// there is no other reading of them anywhere, so this is a first path, not a second one. Headwind
    /// is deliberately absent: resolving it needs the route and the per-point series, and neither
    /// travels, so an analysis reads what the sky did rather than what it did to this rider.
    /// </summary>
    private static AnalysedRide RowToRide(Row row) => new(
        DateOnly.FromDateTime(row.StartTime.Date),
        Math.Round(row.DistanceMeters / 1000.0, 1),
        Math.Round(row.Duration.TotalMinutes, 1),
        row.AverageHeartRate,
        row.MaximumHeartRate,
        row.ElevationGainMeters,
        row.AverageTemperatureCelsius,
        Mean(row.Weather, reading => reading.WindSpeedKmh),
        Total(row.Weather, reading => reading.PrecipitationMm));

    /// <summary>Null rather than zero when nothing was reported: absent is not the same as calm.</summary>
    private static double? Mean(IReadOnlyList<WeatherReading>? weather, Func<WeatherReading, double?> read)
    {
        var values = weather?.Select(read).OfType<double>().ToList();
        return values is { Count: > 0 } ? Math.Round(values.Average(), 1) : null;
    }

    private static double? Total(IReadOnlyList<WeatherReading>? weather, Func<WeatherReading, double?> read)
    {
        var values = weather?.Select(read).OfType<double>().ToList();
        return values is { Count: > 0 } ? values.Sum() : null;
    }
}
