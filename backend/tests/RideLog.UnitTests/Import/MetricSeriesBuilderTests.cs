using RideLog.Application.Routes;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

public sealed class MetricSeriesBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Builds_one_sample_per_point_with_cumulative_distance_elapsed_and_passthrough()
    {
        // Three points 0.01° of latitude apart (~1.11 km each), 30 minutes apart.
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, ElevationMeters: 100, Time: T0, HeartRate: 120),
            new(47.51, 19.0, ElevationMeters: 150, Time: T0.AddMinutes(30), HeartRate: 140),
            new(47.52, 19.0, ElevationMeters: 120, Time: T0.AddMinutes(60), HeartRate: 130),
        };

        var series = MetricSeriesBuilder.Build(points);

        Assert.Equal(3, series.Count);

        // Elapsed comes from the timestamps; elevation and HR pass straight through.
        Assert.Equal(0, series[0].ElapsedMinutes, 0.01);
        Assert.Equal(0, series[0].DistanceKm, 0.01);
        Assert.Equal(100, series[0].ElevationMeters);
        Assert.Equal(120, series[0].HeartRate);

        Assert.Equal(60, series[2].ElapsedMinutes, 0.01);
        Assert.Equal(120, series[2].ElevationMeters);
        Assert.Equal(130, series[2].HeartRate);

        // Cumulative distance is monotonic; two ~1.11 km segments ≈ 2.22 km (independent of the code).
        Assert.True(series[0].DistanceKm <= series[1].DistanceKm && series[1].DistanceKm <= series[2].DistanceKm);
        Assert.Equal(2.22, series[2].DistanceKm, 0.15);
    }

    [Fact]
    public void Downsamples_to_at_most_five_hundred_samples_keeping_the_endpoints()
    {
        var points = Enumerable.Range(0, 1200)
            .Select(i => new GeoPoint(47.5 + i * 0.0001, 19.0, ElevationMeters: i, Time: T0.AddSeconds(i), HeartRate: 100))
            .ToList();

        var series = MetricSeriesBuilder.Build(points);

        Assert.InRange(series.Count, 2, 500);
        // Endpoints preserved: first at distance 0, last carrying the final elevation.
        Assert.Equal(0, series[0].DistanceKm, 0.01);
        Assert.Equal(1199, series[^1].ElevationMeters);
        Assert.True(series[^1].DistanceKm > series[0].DistanceKm);
    }

    [Fact]
    public void Returns_empty_for_no_points()
    {
        Assert.Empty(MetricSeriesBuilder.Build([]));
    }

    [Fact]
    public void Storable_series_is_null_when_no_point_has_elevation_or_heart_rate()
    {
        var barePoints = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0),
            new(47.51, 19.0, Time: T0.AddMinutes(30)),
        };

        Assert.Null(MetricSeriesBuilder.BuildStorable(barePoints));
    }

    [Fact]
    public void Storable_series_is_kept_when_elevation_or_heart_rate_is_present()
    {
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, ElevationMeters: 100, Time: T0),
            new(47.51, 19.0, ElevationMeters: 150, Time: T0.AddMinutes(30)),
        };

        Assert.NotNull(MetricSeriesBuilder.BuildStorable(points));
    }
}
