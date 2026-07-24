namespace RideLog.Application.Rides;

/// <summary>A ride as shown in the list: display-ready units (km, minutes), newest first.</summary>
public sealed record RideListItem
{
    public required Guid Id { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required double DistanceKm { get; init; }
    public required double DurationMinutes { get; init; }
    public required string Sport { get; init; }
    public double? AverageSpeedKmh { get; init; }
    public double? ElevationGainMeters { get; init; }

    /// <summary>Source chips (tokens: PolarAutoSync / PolarImport / Bryton), localized on the client.</summary>
    public required IReadOnlyList<string> Sources { get; init; }
}
