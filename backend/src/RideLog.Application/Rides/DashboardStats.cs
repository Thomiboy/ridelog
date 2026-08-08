namespace RideLog.Application.Rides;

/// <summary>Aggregate totals for one period (this month / this year / last year).</summary>
public sealed record PeriodStats(double DistanceKm, int RideCount, double ElevationGainMeters);

/// <summary>
/// The same calendar month one year ago — the whole month, so the figure is stable rather than
/// changing daily. Year and month are numbers so the label can name the month in the active language.
/// </summary>
public sealed record SameMonthLastYear(int Year, int Month, double DistanceKm, int RideCount);

/// <summary>Distance ridden in one calendar month.</summary>
public sealed record MonthlyDistance(int Year, int Month, double DistanceKm);

/// <summary>Average speed over one calendar month; null when there were no rides.</summary>
public sealed record MonthlySpeed(int Year, int Month, double? AverageSpeedKmh);

/// <summary>Average ridden temperature over one calendar month; null when no ride carried temperature.</summary>
public sealed record MonthlyAverageTemperature(int Year, int Month, double? AverageTemperatureCelsius);

/// <summary>
/// The dashboard's "am I improving?" view: stat tiles plus chart series. Monthly distance covers
/// the current and previous calendar year (12 entries each, zeros included); the speed trend covers
/// the last 12 months ending now.
/// </summary>
public sealed record DashboardStats(
    PeriodStats ThisMonth,
    PeriodStats ThisYear,
    PeriodStats LastYear,
    SameMonthLastYear SameMonthLastYear,
    IReadOnlyList<MonthlyDistance> MonthlyDistance,
    IReadOnlyList<MonthlySpeed> AverageSpeedTrend,
    IReadOnlyList<MonthlyAverageTemperature> AverageTemperatureTrend,
    /// <summary>
    /// Whether this rider has ridden at all, ever. Every other figure here covers this year and
    /// last, so none of them can tell an empty log from one whose rides are simply older.
    /// </summary>
    bool HasRides);
