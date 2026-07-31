using RideLog.Application.Import;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

/// <summary>
/// Parses real exported rides. Invented fixtures kept agreeing with whatever the speed rule happened
/// to do; these files are what a device actually writes — GPS warm-up, stale positions and all.
/// </summary>
public sealed class RealRideFixtureTests
{
    private static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", name);

    private static ParsedActivity Tcx(string name)
    {
        using var stream = File.OpenRead(Path(name));
        return new TcxActivityParser().Parse(stream, name);
    }

    /// <summary>
    /// Every ride here averages 20-26 km/h over 40-60 km, so a top speed in the thirties or forties
    /// is the believable answer and anything near three figures is the recording, not the rider.
    /// The expected values are each device's own summary, except where the track vetoes it.
    /// </summary>
    public static TheoryData<string, double> ExpectedTopSpeeds => new()
    {
        { "berek-2024-05-05.tcx", 33.2 },
        { "berek-2024-05-10.tcx", 30.0 },
        { "berek-2024-05-28.tcx", 31.5 },
        { "berek-2024-08-14.tcx", 37.5 },
        // The one ride whose device is wrong: it claims 85 km/h, which is exactly what its own first
        // seconds of GPS warm-up read. The track supports 44, so the summary is vetoed.
        { "berek-2025-05-31.tcx", 44.0 },
    };

    [Theory]
    [MemberData(nameof(ExpectedTopSpeeds))]
    public void Reports_a_believable_top_speed(string file, double expected)
    {
        var parsed = Tcx(file);

        Assert.Equal(expected, SpeedSeries.TopSpeedKmh(parsed.RoutePoints, parsed.DeviceMaximumSpeedKmh)!.Value, 0.5);
    }

    [Fact]
    public void A_device_summary_is_usually_right_which_is_why_the_track_only_vetoes()
    {
        // Four of these five devices summarised a believable maximum and one did not, while the
        // track-derived figure was wrong on four of the five. Neither source can be trusted alone.
        Assert.Equal(33.2, Tcx("berek-2024-05-05.tcx").DeviceMaximumSpeedKmh!.Value, 0.1);
        Assert.Equal(85.0, Tcx("berek-2025-05-31.tcx").DeviceMaximumSpeedKmh!.Value, 0.1);

        // GPS noise on this device runs to three figures, so the track alone is no better.
        Assert.InRange(SpeedSeries.MaxKmh(Tcx("berek-2024-05-28.tcx").RoutePoints)!.Value, 100, 200);
    }

    [Fact]
    public void The_vetoed_rides_peak_is_sustained_rather_than_a_single_sample()
    {
        // The track only gets to overrule a device when its own figure is worth more, so that figure
        // has to be corroborated: the rider held it, and the samples around it agree.
        var points = Tcx("berek-2025-05-31.tcx").RoutePoints;
        var resolved = SpeedSeries.Resolve(points);
        var peak = SpeedSeries.MaxKmh(points)!.Value;
        var peakIndex = resolved.Select((s, i) => (s, i)).First(x => x.s == peak).i;

        var neighbours = resolved.Skip(peakIndex - 5).Take(11).Count(s => s is { } kmh && kmh > peak - 1);

        Assert.True(neighbours >= 5, $"the peak of {peak:0.0} km/h stands alone, which reads as a spike");
    }

    [Fact]
    public void A_real_bryton_fit_from_late_july_reads_like_late_july()
    {
        // Recorded 2026-07-25. A summary drawn from every record message rather than from the track
        // used to report a 0 °C low here, from the seconds before the device had a GPS fix.
        using var stream = File.OpenRead(Path("260725070607.fit"));
        var parsed = new FitActivityParser().Parse(stream, "260725070607.fit");

        Assert.Equal(15, parsed.MinTemperatureCelsius!.Value, 0.01);
        Assert.Equal(29, parsed.MaxTemperatureCelsius!.Value, 0.01);
        Assert.All(parsed.RoutePoints, p => Assert.True(p.TemperatureCelsius is null or >= 15));
    }
}
