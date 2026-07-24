namespace RideLog.Domain.Rides;

/// <summary>
/// One downsampled point of a ride's metric series, for the elevation/HR/temperature graph. Carries
/// both a cumulative-distance and an elapsed-time coordinate so the chart can switch its x-axis
/// without a refetch. Elevation, heart rate and temperature are null where the source didn't record
/// them (temperature comes from a merged Bryton FIT).
/// </summary>
public sealed record MetricSample(
    double DistanceKm, double ElapsedMinutes, double? ElevationMeters, int? HeartRate, double? TemperatureCelsius = null);
