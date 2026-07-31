using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Rides;
using RideLog.Infrastructure.Weather;

namespace RideLog.UnitTests.Weather;

/// <summary>
/// Read against a real ride and the real archive response for its day: Szeged, 2025-05-31, out to
/// 31 km and back over 62 km, 06:23–08:45 UTC. Wind that morning was light and westerly — 4.0 km/h
/// from 273°, then 4.7 from 254°, then 6.9 from 250° — and the rider remembers the run home being
/// pushed along, which is the fact these tests are anchored to.
/// </summary>
public sealed class RideWeatherReaderTests
{
    private static readonly DateTimeOffset Start = new(2025, 5, 31, 6, 23, 10, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2025, 5, 31, 8, 45, 9, TimeSpan.Zero);

    private static RideWeather Read()
    {
        using var stream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "berek-2025-05-31.tcx"));
        var parsed = new TcxActivityParser().Parse(stream, "berek-2025-05-31.tcx");

        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Weather", "Fixtures", "open-meteo-2025-05-31-szeged.json"));
        var readings = OpenMeteoResponseReader.Read(json, Start, End);

        var series = MetricSeriesBuilder.BuildStorable(parsed.RoutePoints, parsed.DeviceMaximumSpeedKmh);
        var polyline = PolylineEncoder.Encode(parsed.RoutePoints);

        return RideWeatherReader.Read(readings, polyline, series, Start)!;
    }

    /// <summary>
    /// The rider turned for home at 31 km, two thirds of the way through the 07:00 hour. Taking that
    /// hour's direction from where it started and where it ended asks which way a rider went when
    /// they went out and came back — the two points nearly coincide, and the leftover is noise. It
    /// answered "tailwind" for an hour that, honestly weighed, was neither.
    /// </summary>
    [Fact]
    public void Does_not_invent_a_direction_for_the_hour_the_rider_turned_around_in()
    {
        var turnaroundHour = Read().Hours.Single(hour => hour.Hour.Hour == 7);

        Assert.NotNull(turnaroundHour.HeadwindKmh);
        Assert.True(
            Math.Abs(turnaroundHour.HeadwindKmh!.Value) < 0.5,
            $"an hour split between out and back should net out near zero, got {turnaroundHour.HeadwindKmh}");
    }

    /// <summary>
    /// The last stretch home ran with the wind squarely behind — near the full 6.9 km/h the hour
    /// reported. An hourly figure buries that: the 08:00 hour also contains the twisting run up to
    /// it, and averaging the two called the whole hour a headwind. Per sample, the ride tells the
    /// truth about where the rider was pushed.
    /// </summary>
    [Fact]
    public void Shows_the_run_home_as_the_tailwind_it_was()
    {
        var weather = Read();
        var lastQuarter = weather.HeadwindKmhBySample
            .Skip(weather.HeadwindKmhBySample.Count * 3 / 4)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        Assert.NotEmpty(lastQuarter);
        Assert.True(
            lastQuarter.Min() < -4,
            $"the run home was pushed along by most of a 6.9 km/h wind, got a minimum of {lastQuarter.Min():F1}");
    }
}
