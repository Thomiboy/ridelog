namespace RideLog.Application.Rides;

/// <summary>
/// One hour of a ride's weather as the detail view needs it: what the service reported, plus the
/// one thing only this ride can say about it — how much of that wind was actually in the rider's
/// face. Reported values are stored; <see cref="HeadwindKmh"/> is worked out on read from the route,
/// so correcting the calculation never means fetching anything again (docs/adr/0005).
/// </summary>
/// <param name="WindFromBearing">Degrees clockwise from north, naming where the wind blew *from*.</param>
/// <param name="HeadwindKmh">
/// Positive into the wind, negative with it behind, near zero across it. Null when the ride has no
/// route to take a direction from, or the hour reported no wind.
/// </param>
public sealed record WeatherHour(
    DateTimeOffset Hour,
    double? TemperatureCelsius,
    double? WindSpeedKmh,
    double? WindFromBearing,
    double? HeadwindKmh,
    double? PrecipitationMm,
    int? RelativeHumidityPercent,
    int? CloudCoverPercent,
    int? WeatherCode);
