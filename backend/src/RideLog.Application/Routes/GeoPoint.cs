namespace RideLog.Application.Routes;

/// <summary>A single recorded track point: position plus optional elevation, timestamp and heart rate.</summary>
public readonly record struct GeoPoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters = null,
    DateTimeOffset? Time = null,
    int? HeartRate = null);
