namespace RideLog.Application.Rides;

/// <summary>Aggregate totals for one period (this month / this year).</summary>
public sealed record PeriodStats(double DistanceKm, int RideCount, double ElevationGainMeters);

/// <summary>Distance ridden in one calendar month.</summary>
public sealed record MonthlyDistance(int Year, int Month, double DistanceKm);

/// <summary>Average speed over one calendar month; null when there were no rides.</summary>
public sealed record MonthlySpeed(int Year, int Month, double? AverageSpeedKmh);

/// <summary>
/// The dashboard's "am I improving?" view: stat tiles plus chart series. Monthly distance covers
/// the current and previous calendar year (12 entries each, zeros included); the speed trend covers
/// the last 12 months ending now.
/// </summary>
public sealed record DashboardStats(
    PeriodStats ThisMonth,
    PeriodStats ThisYear,
    IReadOnlyList<MonthlyDistance> MonthlyDistance,
    IReadOnlyList<MonthlySpeed> AverageSpeedTrend);
