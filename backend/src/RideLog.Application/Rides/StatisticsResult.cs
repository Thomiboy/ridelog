namespace RideLog.Application.Rides;

/// <summary>Totals for one calendar month of cycling; only months with rides are emitted.</summary>
public sealed record MonthlyAggregate(
    int Year, int Month, double DistanceKm, double ElevationGainMeters, int RideCount, int Calories,
    double DurationMinutes);

/// <summary>The single ride with the greatest distance; links back to that ride.</summary>
public sealed record LongestRideRecord(Guid Id, DateTimeOffset Date, double DistanceKm);

/// <summary>
/// The ride with the highest average speed among rides of at least
/// <see cref="StatisticsRecords.FastestAverageMinimumKm"/> km (the threshold keeps a short sprint
/// from skewing the record); links back to that ride.
/// </summary>
public sealed record FastestAverageRecord(Guid Id, DateTimeOffset Date, double AverageSpeedKmh);

/// <summary>The longest run of consecutive calendar days that each had at least one cycling ride.</summary>
public sealed record StreakRecord(int Days, DateOnly StartDate, DateOnly EndDate, double DistanceKm);

/// <summary>The single ride that burned the most calories; links back to that ride.</summary>
public sealed record MostCaloriesRecord(Guid Id, DateTimeOffset Date, int Calories);

/// <summary>The single ride with the greatest moving duration (minutes); links back to that ride.</summary>
public sealed record LongestDurationRecord(Guid Id, DateTimeOffset Date, double DurationMinutes);

/// <summary>
/// The calendar month that rode the most distance. Year and month are numbers so the frontend can
/// format the month name in the active language.
/// </summary>
public sealed record BestMonthDistanceRecord(int Year, int Month, double DistanceKm);

/// <summary>The calendar month with the most rides; year and month are numbers, as above.</summary>
public sealed record BestMonthRidesRecord(int Year, int Month, int RideCount);

/// <summary>The single ride that reached the highest speed; links back to that ride.</summary>
public sealed record MaxSpeedRecord(Guid Id, DateTimeOffset Date, double MaxSpeedKmh);

/// <summary>The single ride with the greatest elevation gain; links back to that ride.</summary>
public sealed record BiggestClimbRecord(Guid Id, DateTimeOffset Date, double ElevationGainMeters);

/// <summary>Personal records for the Records section.</summary>
public sealed record StatisticsRecords(
    LongestRideRecord? LongestRide,
    FastestAverageRecord? FastestAverage,
    StreakRecord? LongestStreak,
    MostCaloriesRecord? MostCalories,
    LongestDurationRecord? LongestDuration,
    BestMonthDistanceRecord? BestMonthDistance,
    BestMonthRidesRecord? BestMonthRides,
    MaxSpeedRecord? MaxSpeed,
    BiggestClimbRecord? BiggestClimb)
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
