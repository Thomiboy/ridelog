namespace RideLog.Application.Routes;

/// <summary>A single recorded track point: position plus optional elevation, timestamp, heart rate and temperature.</summary>
public readonly record struct GeoPoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters = null,
    DateTimeOffset? Time = null,
    int? HeartRate = null,
    double? TemperatureCelsius = null);
