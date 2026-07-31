namespace RideLog.Domain.Rides;

/// <summary>
/// One hour of weather reported for the area a ride passed through. Reported, not measured: this is
/// what a weather service says about the place and hour, never what the bike itself recorded — see
/// docs/adr/0005. Every field is optional because a service may cover an hour only partly.
/// </summary>
/// <param name="Hour">Start of the hour the reading covers.</param>
/// <param name="WindFromBearing">Degrees clockwise from north, naming where the wind blows *from*.</param>
public readonly record struct WeatherReading(
    DateTimeOffset Hour,
    double? TemperatureCelsius,
    double? WindSpeedKmh,
    double? WindFromBearing,
    double? PrecipitationMm,
    int? RelativeHumidityPercent,
    int? CloudCoverPercent,
    int? WeatherCode);

/// <summary>
/// How a ride's weather lookup ended. Recorded so the daily top-up can tell a ride it has not tried
/// from one it has: without it, rides that can never succeed would be retried every morning.
/// </summary>
public enum WeatherOutcome
{
    /// <summary>Readings were returned and stored.</summary>
    Fetched,

    /// <summary>The service has no data for that place and time, and never will. Do not retry.</summary>
    Unavailable,

    /// <summary>The lookup failed for a reason that may not recur. Worth retrying.</summary>
    Failed,
}
