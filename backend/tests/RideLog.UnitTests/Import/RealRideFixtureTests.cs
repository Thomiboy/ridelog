using RideLog.Application.Import;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

/// <summary>
/// Parses real exported rides. Invented fixtures kept agreeing with whatever the speed rule happened
/// to do; these files are what a device actually writes — GPS warm-up, stale positions and all.
/// </summary>
public sealed class RealRideFixtureTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", name);

    /// <summary>A flat 11.4 km ride recorded at 1 Hz; its own lap summary says 10.4 m/s = 37.5 km/h.</summary>
    private static ParsedActivity Berek()
    {
        using var stream = File.OpenRead(Fixture("berek-2024-08-14.tcx"));
        return new TcxActivityParser().Parse(stream, "berek-2024-08-14.tcx");
    }

    [Fact]
    public void Berek_ride_reports_a_top_speed_its_own_device_agrees_with()
    {
        var parsed = Berek();

        // The device measured 37.5 km/h with its own sensor — an independent reading of the same
        // ride, and the only trustworthy check on a number we derive from positions. Before the
        // opening reading was judged properly this came out at 190 km/h.
        Assert.Equal(37.5, parsed.DeviceMaximumSpeedKmh!.Value, 0.1);
        Assert.InRange(SpeedSeries.MaxKmh(parsed.RoutePoints)!.Value, 30, 40);
    }

    [Fact]
    public void Berek_ride_has_no_speed_until_the_gps_has_settled()
    {
        // The recording starts with 23 positionless records, then two intervals of a fix still
        // jumping around — 190 km/h followed by 770 km/h. Neither may reach the ride.
        var resolved = SpeedSeries.Resolve(Berek().RoutePoints);

        Assert.All(resolved.Take(3), speed => Assert.Null(speed));
        Assert.NotNull(resolved[3]);
    }

    [Fact]
    public void Berek_ride_keeps_most_of_its_readings()
    {
        // This device repeats the previous position on roughly a fifth of its samples, so the
        // per-second speed alternates between a standstill and double the real pace, and the filter
        // has plenty to reject. It must still leave enough of the ride to graph.
        var resolved = SpeedSeries.Resolve(Berek().RoutePoints);

        Assert.True(
            resolved.Count(s => s is not null) > resolved.Count * 0.7,
            "the filter discarded more than a third of a legitimate ride");
    }
}
