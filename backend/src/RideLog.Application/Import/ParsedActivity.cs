using RideLog.Application.Routes;

namespace RideLog.Application.Import;

/// <summary>Summary metrics and route extracted from a single uploaded activity file.</summary>
public sealed record ParsedActivity
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required TimeSpan Duration { get; init; }
    public required double DistanceMeters { get; init; }
    public required string Sport { get; init; }

    public double? AverageSpeedKmh { get; init; }
    public double? MaximumSpeedKmh { get; init; }
    public int? AverageHeartRate { get; init; }
    public int? MaximumHeartRate { get; init; }
    public double? ElevationGainMeters { get; init; }
    public int? AverageCadence { get; init; }

    public required IReadOnlyList<GeoPoint> RoutePoints { get; init; }
}
