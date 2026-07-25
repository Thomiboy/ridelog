using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

public class RestStopDetectorTests
{
    private static MetricSample Sample(double km, double minutes) => new(km, minutes, null, null);

    // Two points one degree of longitude apart on the equator. The great-circle arc length there is
    // R·Δλ = 6_371_000 · π/180 ≈ 111.195 km — a closed-form value independent of the haversine code.
    private static readonly GeoPoint[] EquatorLeg = [new(0, 0), new(0, 1)];
    private const double EquatorLegKm = 6_371_000 * Math.PI / 180 / 1000;

    [Fact]
    public void Positions_a_distance_by_interpolating_along_the_route()
    {
        var mid = RestStopDetector.PositionAtDistanceKm(EquatorLeg, EquatorLegKm / 2);

        // Halfway along the leg is halfway in longitude, still on the equator.
        Assert.Equal(0, mid.Latitude, 3);
        Assert.Equal(0.5, mid.Longitude, 3);
    }

    [Fact]
    public void Clamps_positions_to_the_ends_of_the_route()
    {
        Assert.Equal(0, RestStopDetector.PositionAtDistanceKm(EquatorLeg, 0).Longitude, 6); // start
        Assert.Equal(1, RestStopDetector.PositionAtDistanceKm(EquatorLeg, 999).Longitude, 6); // past the end → last point
    }

    [Fact]
    public void Detects_a_gap_over_a_minute_with_little_movement_as_a_rest()
    {
        var series = new[]
        {
            Sample(0.0, 0),
            Sample(5.0, 15),
            // Paused here: 3 minutes pass but distance barely moves (10 m).
            Sample(5.01, 18),
            Sample(10.0, 33),
        };

        var rests = RestStopDetector.DetectRestDistancesKm(series);

        // One rest, at the distance where the pause began.
        Assert.Equal([5.0], rests);
    }

    [Fact]
    public void Ignores_a_brief_pause_and_normal_riding()
    {
        var series = new[]
        {
            Sample(0.0, 0),
            Sample(5.0, 15),      // riding: distance advances with time
            Sample(5.005, 15.5),  // only 30 s pause → not a rest
            Sample(10.0, 30),
        };

        Assert.Empty(RestStopDetector.DetectRestDistancesKm(series));
    }

    [Fact]
    public void Collapses_consecutive_paused_samples_into_one_rest()
    {
        var series = new[]
        {
            Sample(0.0, 0),
            Sample(5.0, 15),
            Sample(5.01, 17),  // paused (2 min, ~10 m)
            Sample(5.02, 19),  // still paused (another 2 min, ~10 m)
            Sample(10.0, 34),
        };

        // The two adjacent paused gaps are one stop, recorded at where it began.
        Assert.Equal([5.0], RestStopDetector.DetectRestDistancesKm(series));
    }
}
