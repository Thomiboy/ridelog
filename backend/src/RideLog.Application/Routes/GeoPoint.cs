namespace RideLog.Application.Routes;

/// <summary>A single recorded track point: position plus optional elevation and timestamp.</summary>
public readonly record struct GeoPoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters = null,
    DateTimeOffset? Time = null);
