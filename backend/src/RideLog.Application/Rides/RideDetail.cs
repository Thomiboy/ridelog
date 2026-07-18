namespace RideLog.Application.Rides;

/// <summary>Full detail for one ride: display-ready metrics, the source badge, and the encoded route.</summary>
public sealed record RideDetail
{
    public required Guid Id { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required double DistanceKm { get; init; }
    public required double DurationMinutes { get; init; }
    public required string Sport { get; init; }
    public required string Source { get; init; }

    public double? AverageSpeedKmh { get; init; }
    public double? MaximumSpeedKmh { get; init; }
    public int? AverageHeartRate { get; init; }
    public int? MaximumHeartRate { get; init; }
    public double? ElevationGainMeters { get; init; }
    public int? AverageCadence { get; init; }
    public int? Calories { get; init; }

    public string? RoutePolyline { get; init; }
}
