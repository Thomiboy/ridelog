using System.Globalization;
using System.Text.Json;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Weather;

/// <summary>
/// Turns an Open-Meteo archive response into the hours a ride actually touched. Kept apart from the
/// HTTP call so it can be read against a real recorded response; the client above it only fetches.
/// </summary>
public static class OpenMeteoResponseReader
{
    /// <summary>
    /// Readings for every hour whose window overlaps the ride. The archive answers in whole days, so
    /// most of what comes back belongs to hours nobody rode.
    /// </summary>
    public static IReadOnlyList<WeatherReading> Read(string json, DateTimeOffset from, DateTimeOffset to)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("hourly", out var hourly)
            || !hourly.TryGetProperty("time", out var times))
        {
            return [];
        }

        var temperature = Channel(hourly, "temperature_2m");
        var windSpeed = Channel(hourly, "wind_speed_10m");
        var windDirection = Channel(hourly, "wind_direction_10m");
        var precipitation = Channel(hourly, "precipitation");
        var humidity = Channel(hourly, "relative_humidity_2m");
        var cloudCover = Channel(hourly, "cloud_cover");
        var weatherCode = Channel(hourly, "weather_code");

        var readings = new List<WeatherReading>();
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            var hour = ParseHour(times[i].GetString());
            if (hour is null || !Overlaps(hour.Value, from, to))
            {
                continue;
            }

            readings.Add(new WeatherReading(
                hour.Value,
                At(temperature, i),
                At(windSpeed, i),
                At(windDirection, i),
                At(precipitation, i),
                (int?)At(humidity, i),
                (int?)At(cloudCover, i),
                (int?)At(weatherCode, i)));
        }

        return readings;
    }

    /// <summary>An hour belongs to the ride when the hour it labels overlaps the ride's window.</summary>
    private static bool Overlaps(DateTimeOffset hour, DateTimeOffset from, DateTimeOffset to)
        => hour < to && hour.AddHours(1) > from;

    /// <summary>
    /// The response times carry no offset ("2024-05-05T06:00") even though the request asked for UTC
    /// and the body says utc_offset_seconds 0. Parsed as local they would shift by the running
    /// machine's zone and still look entirely plausible, so UTC is asserted here rather than assumed.
    /// </summary>
    private static DateTimeOffset? ParseHour(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var naive)
            ? new DateTimeOffset(DateTime.SpecifyKind(naive, DateTimeKind.Utc))
            : null;

    private static JsonElement? Channel(JsonElement hourly, string name)
        => hourly.TryGetProperty(name, out var channel) ? channel : null;

    /// <summary>A channel may be absent entirely, or present with a null for an hour it doesn't cover.</summary>
    private static double? At(JsonElement? channel, int index)
    {
        if (channel is not { } values || index >= values.GetArrayLength())
        {
            return null;
        }

        var value = values[index];
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }
}
