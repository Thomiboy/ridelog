namespace RideLog.Application.Rides;

/// <summary>Totals for one calendar month of cycling; only months with rides are emitted.</summary>
public sealed record MonthlyAggregate(
    int Year, int Month, double DistanceKm, double ElevationGainMeters, int RideCount, int Calories);

/// <summary>The single ride with the greatest distance; links back to that ride.</summary>
public sealed record LongestRideRecord(Guid Id, DateTimeOffset Date, double DistanceKm);

/// <summary>
/// The ride with the highest average speed among rides of at least
/// <see cref="StatisticsRecords.FastestAverageMinimumKm"/> km (the threshold keeps a short sprint
/// from skewing the record); links back to that ride.
/// </summary>
public sealed record FastestAverageRecord(Guid Id, DateTimeOffset Date, double AverageSpeedKmh);

/// <summary>The longest run of consecutive calendar days that each had at least one cycling ride.</summary>
public sealed record StreakRecord(int Days, DateOnly StartDate, DateOnly EndDate);

/// <summary>Personal records for the Records section.</summary>
public sealed record StatisticsRecords(
    LongestRideRecord? LongestRide,
    FastestAverageRecord? FastestAverage,
    StreakRecord? LongestStreak)
{
    /// <summary>Minimum distance a ride must cover to qualify for the fastest-average record.</summary>
    public const double FastestAverageMinimumKm = 30.0;
}

/// <summary>
/// The Statistics page's feed. All-years monthly aggregates drive the Trends charts (the frontend
/// filters to the selected year and derives the year-over-year totals client-side); Records feeds
/// the Records section.
/// </summary>
public sealed record StatisticsResult(
    IReadOnlyList<MonthlyAggregate> MonthlyAggregates,
    StatisticsRecords Records,
    IReadOnlyList<HrZoneSlice>? HrZones,
    TemperatureStats? Temperature);
