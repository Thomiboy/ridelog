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

    /// <summary>
    /// The maximum speed the device wrote into its own summary, exactly as the file states it — a
    /// single GPS jump lands here and stays, so this is <em>not</em> the ride's top speed and must
    /// never be stored as one. Derive that from the route the ride actually keeps and fall back to
    /// this only when that route yields no speed at all (docs/adr/0002).
    /// </summary>
    public double? DeviceMaximumSpeedKmh { get; init; }
    public int? AverageHeartRate { get; init; }
    public int? MaximumHeartRate { get; init; }
    public double? ElevationGainMeters { get; init; }
    public int? AverageCadence { get; init; }
    public int? Calories { get; init; }

    /// <summary>Ambient temperature from the device's series (Bryton FIT); null when not recorded.</summary>
    public double? AverageTemperatureCelsius { get; init; }
    public double? MinTemperatureCelsius { get; init; }
    public double? MaxTemperatureCelsius { get; init; }

    public required IReadOnlyList<GeoPoint> RoutePoints { get; init; }
}
