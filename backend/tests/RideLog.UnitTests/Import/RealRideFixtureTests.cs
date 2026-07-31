using RideLog.Application.Import;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

/// <summary>
/// Parses real exported rides. Invented fixtures kept agreeing with whatever the speed rule happened
/// to do; these files are what a device actually writes — GPS warm-up, stale positions and all.
/// </summary>
public sealed class RealRideFixtureTests
{
    private static ParsedActivity Parse(string name)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", name));
        return new TcxActivityParser().Parse(stream, name);
    }

    /// <summary>A flat 11.4 km ride at 1 Hz. Its lap summary says 10.4 m/s = 37.5 km/h.</summary>
    private static ParsedActivity Short() => Parse("berek-2024-08-14.tcx");

    /// <summary>A 62 km ride at 1 Hz, averaging 26 km/h. Its lap summary says 23.6 m/s = 85 km/h.</summary>
    private static ParsedActivity Long() => Parse("berek-2025-05-31.tcx");

    [Fact]
    public void A_devices_own_maximum_cannot_be_trusted_as_a_cross_check()
    {
        // These two rides come from the same device. One summarised a believable 37.5 km/h; the
        // other claims 85 km/h on a ride averaging 26 — and 85 is exactly what its own first few
        // seconds of GPS warm-up read. That is the whole reason the track decides (docs/adr/0002):
        // the summary is sometimes right, which is worse than being reliably wrong.
        Assert.Equal(37.5, Short().DeviceMaximumSpeedKmh!.Value, 0.1);
        Assert.Equal(85.0, Long().DeviceMaximumSpeedKmh!.Value, 0.1);
    }

    [Fact]
    public void Neither_ride_takes_its_top_speed_from_gps_warm_up()
    {
        // Both recordings open with a fix still settling: 190 then 770 km/h on the short ride, 77 to
        // 129 km/h across the long one's first twenty seconds. None of it may reach the ride.
        Assert.InRange(SpeedSeries.MaxKmh(Short().RoutePoints)!.Value, 30, 40);
        Assert.InRange(SpeedSeries.MaxKmh(Long().RoutePoints)!.Value, 40, 50);
    }

    [Fact]
    public void The_long_rides_peak_is_sustained_rather_than_a_single_sample()
    {
        // The check that matters in the other direction: that a real peak survives. This one is not
        // one lucky sample — the rider held it, so the samples around it agree. A rule that shaved
        // genuine peaks would leave an isolated maximum instead.
        var resolved = SpeedSeries.Resolve(Long().RoutePoints);
        var peak = SpeedSeries.MaxKmh(Long().RoutePoints)!.Value;
        var peakIndex = resolved.Select((s, i) => (s, i)).First(x => x.s == peak).i;

        var neighbours = resolved
            .Skip(peakIndex - 5)
            .Take(11)
            .Count(s => s is { } kmh && kmh > peak - 1);

        Assert.True(neighbours >= 5, $"the peak of {peak:0.0} km/h stands alone, which reads as a spike");
    }

    [Fact]
    public void Both_rides_keep_enough_readings_to_graph()
    {
        // The short ride's device repeats the previous position on about a fifth of its samples, so
        // its per-second speed alternates between a standstill and double the real pace and the
        // filter has plenty to reject. Even there it must leave most of the ride intact.
        foreach (var ride in new[] { Short(), Long() })
        {
            var resolved = SpeedSeries.Resolve(ride.RoutePoints);
            Assert.True(
                resolved.Count(s => s is not null) > resolved.Count * 0.7,
                "the filter discarded more than a third of a legitimate ride");
        }
    }

    [Fact]
    public void A_real_bryton_fit_from_late_july_reads_like_late_july()
    {
        // Recorded 2026-07-25. A summary drawn from every record message rather than from the track
        // used to report a 0 °C low here, from the seconds before the device had a GPS fix.
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "260725070607.fit"));
        var parsed = new FitActivityParser().Parse(stream, "260725070607.fit");

        Assert.Equal(15, parsed.MinTemperatureCelsius!.Value, 0.01);
        Assert.Equal(29, parsed.MaxTemperatureCelsius!.Value, 0.01);
        Assert.All(parsed.RoutePoints, p => Assert.True(p.TemperatureCelsius is null or >= 15));
    }
}
