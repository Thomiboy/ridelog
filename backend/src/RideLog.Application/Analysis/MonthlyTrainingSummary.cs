using RideLog.Application.Rides;

namespace RideLog.Application.Analysis;

/// <summary>
/// One ride of the month, as much of it as may leave the app. Deliberately no id: an analysis is
/// about a month, and naming the rides inside it would put a handle on this rider's log into a
/// third party's hands for nothing in return.
/// </summary>
/// <param name="WindSpeedKmh">Mean of the hours this ride ran through; null when it stored no weather.</param>
/// <param name="PrecipitationMm">Total across those hours; null likewise.</param>
public sealed record AnalysedRide(
    DateOnly Date,
    double DistanceKm,
    double DurationMinutes,
    int? AverageHeartRate,
    int? MaximumHeartRate,
    double? ElevationGainMeters,
    double? AverageTemperatureCelsius,
    double? WindSpeedKmh,
    double? PrecipitationMm);

/// <summary>
/// Everything a monthly analysis is written from — and, by being the only argument
/// <c>ITrainingAnalyst</c> takes, everything that can possibly leave (#187, docs/adr/0008).
///
/// There is nowhere in this type to put a rider id, a name, an address, a ride id, a coordinate or a
/// route. That is the guarantee: not a rule someone has to remember at the call site, but a fact
/// about the type, the same way <c>IOwnerMailSender.NotifyOwnerAsync</c> takes no recipient (#168).
///
/// The figures are the ones the Statistics page already computes. The model reads them; it never
/// works any of them out for itself, because two paths to one number is how they start disagreeing
/// (docs/adr/0003) — and here the second path would be one nobody can inspect.
/// </summary>
/// <param name="Month">The month under analysis; carries its own year and month number.</param>
/// <param name="PrecedingMonths">
/// Up to the twelve months before it, oldest first, so a month can be read against the year that led
/// to it. Months with no cycling are simply absent.
/// </param>
public sealed record MonthlyTrainingSummary(
    MonthlyAggregate Month,
    IReadOnlyList<MonthlyAggregate> PrecedingMonths,
    IReadOnlyList<AnalysedRide> Rides,
    IReadOnlyList<HrZoneSlice>? HrZones,
    IReadOnlyList<TemperatureBandSlice>? TemperatureBands)
{
    /// <summary>How many months of history travel with the month itself.</summary>
    public const int PrecedingMonthCount = 12;
}
