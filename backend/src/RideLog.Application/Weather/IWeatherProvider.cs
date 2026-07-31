using RideLog.Domain.Rides;

namespace RideLog.Application.Weather;

/// <summary>The outcome of one lookup, plus whatever it returned.</summary>
public sealed record WeatherLookup(WeatherOutcome Outcome, IReadOnlyList<WeatherReading> Readings)
{
    public static WeatherLookup Fetched(IReadOnlyList<WeatherReading> readings) =>
        new(WeatherOutcome.Fetched, readings);

    /// <summary>No data for that place and time, and there never will be — do not ask again.</summary>
    public static WeatherLookup Unavailable { get; } = new(WeatherOutcome.Unavailable, []);

    /// <summary>The lookup failed in a way that may not recur — worth asking again later.</summary>
    public static WeatherLookup Failed { get; } = new(WeatherOutcome.Failed, []);
}

/// <summary>
/// Historical weather for a place and time window. Implementations must distinguish "no data, ever"
/// from "failed this time": the two lead to opposite decisions about retrying.
/// </summary>
public interface IWeatherProvider
{
    Task<WeatherLookup> GetHourlyAsync(
        double latitude,
        double longitude,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
