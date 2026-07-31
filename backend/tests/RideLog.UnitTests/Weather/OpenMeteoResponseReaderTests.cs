using RideLog.Infrastructure.Weather;

namespace RideLog.UnitTests.Weather;

/// <summary>
/// Read against a real archive response for one of the committed fixture rides (Szeged,
/// 2024-05-05), for the reason the import fixtures exist: an invented response agrees with whatever
/// the parser happens to do. This one carries two things nobody would have thought to invent.
/// </summary>
public sealed class OpenMeteoResponseReaderTests
{
    private static string Response() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Weather", "Fixtures", "open-meteo-2024-05-05-szeged.json"));

    // The ride runs 06:52:56Z → 09:32:02Z, so it touches four of the day's twenty-four hours: the
    // one it started inside, the two it rode through, and the one it finished inside.
    private static readonly DateTimeOffset RideStart = new(2024, 5, 5, 6, 52, 56, TimeSpan.Zero);
    private static readonly DateTimeOffset RideEnd = new(2024, 5, 5, 9, 32, 2, TimeSpan.Zero);

    [Fact]
    public void Keeps_only_the_hours_the_ride_touches()
    {
        var readings = OpenMeteoResponseReader.Read(Response(), RideStart, RideEnd);

        Assert.Equal(
            [Utc(6), Utc(7), Utc(8), Utc(9)],
            readings.Select(reading => reading.Hour));
    }

    // The response labels its hours "2024-05-05T00:00" — no offset, no Z — while separately saying
    // utc_offset_seconds 0. Parsed naively those become whatever the machine's local time is, which
    // on a CI box in another zone would silently shift every reading and still look plausible.
    [Fact]
    public void Reads_the_unsuffixed_timestamps_as_utc()
    {
        var readings = OpenMeteoResponseReader.Read(Response(), RideStart, RideEnd);

        Assert.All(readings, reading => Assert.Equal(TimeSpan.Zero, reading.Hour.Offset));
        Assert.Equal(new DateTimeOffset(2024, 5, 5, 6, 0, 0, TimeSpan.Zero), readings[0].Hour);
    }

    [Fact]
    public void Carries_every_channel_across_for_the_hour_the_ride_started_in()
    {
        var first = OpenMeteoResponseReader.Read(Response(), RideStart, RideEnd)[0];

        Assert.Equal(17.0, first.TemperatureCelsius);
        Assert.Equal(6.9, first.WindSpeedKmh);
        Assert.Equal(231, first.WindFromBearing);
        Assert.Equal(0.0, first.PrecipitationMm);
        Assert.Equal(83, first.RelativeHumidityPercent);
        Assert.Equal(0, first.CloudCoverPercent);
        Assert.Equal(0, first.WeatherCode);
    }

    private static DateTimeOffset Utc(int hour) => new(2024, 5, 5, hour, 0, 0, TimeSpan.Zero);
}
