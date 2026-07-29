namespace RideLog.Application.Routes;

/// <summary>
/// A single recorded track point: position plus optional elevation, timestamp, heart rate,
/// temperature and the device's own speed reading (km/h) where the source records one.
/// </summary>
public readonly record struct GeoPoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters = null,
    DateTimeOffset? Time = null,
    int? HeartRate = null,
    double? TemperatureCelsius = null,
    double? SpeedKmh = null);
