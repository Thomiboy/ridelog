using System.Globalization;
using Microsoft.Extensions.Logging;
using RideLog.Application.Weather;

namespace RideLog.Infrastructure.Weather;

/// <summary>
/// Open-Meteo's archive endpoint: free for non-commercial use and needs no key, which is what keeps
/// this within the zero-cost hosting rule. Deliberately thin — it fetches and hands the body to
/// <see cref="OpenMeteoResponseReader"/>, which is what carries the logic and what the tests read
/// against a real recorded response.
/// </summary>
public sealed class OpenMeteoWeatherProvider(HttpClient client, ILogger<OpenMeteoWeatherProvider> logger)
    : IWeatherProvider
{
    private const string Hourly =
        "temperature_2m,wind_speed_10m,wind_direction_10m,precipitation,relative_humidity_2m,cloud_cover,weather_code";

    public async Task<WeatherLookup> GetHourlyAsync(
        double latitude,
        double longitude,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        // The archive answers in whole days, so the window is asked for by date and trimmed by the
        // reader. A ride that runs past midnight spans two.
        var url = "v1/archive"
                  + $"?latitude={latitude.ToString("0.#####", CultureInfo.InvariantCulture)}"
                  + $"&longitude={longitude.ToString("0.#####", CultureInfo.InvariantCulture)}"
                  + $"&start_date={from.UtcDateTime:yyyy-MM-dd}"
                  + $"&end_date={to.UtcDateTime:yyyy-MM-dd}"
                  + $"&hourly={Hourly}"
                  + "&wind_speed_unit=kmh"
                  + "&timezone=UTC";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Open-Meteo answered {Status} for {Latitude},{Longitude} on {Date}",
                (int)response.StatusCode, latitude, longitude, from.UtcDateTime.ToString("yyyy-MM-dd"));
            return WeatherLookup.Failed;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var readings = OpenMeteoResponseReader.Read(body, from, to);

        // An answer with nothing in it is not an error: the archive simply does not cover those
        // hours. Whether that is permanent is the caller's call, since only it knows the ride's age.
        return readings.Count > 0 ? WeatherLookup.Fetched(readings) : WeatherLookup.Unavailable;
    }
}
