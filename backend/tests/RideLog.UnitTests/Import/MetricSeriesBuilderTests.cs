using RideLog.Application.Routes;
using RideLog.Domain.Rides;
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
    public void Derives_speed_from_distance_and_time_when_the_source_records_none()
    {
        // 0.01° of latitude ≈ 1.11 km; covered in 30 minutes → ≈ 2.22 km/h. The expected value comes
        // from the coordinates and the clock, not from the builder.
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0),
            new(47.51, 19.0, Time: T0.AddMinutes(30)),
        };

        var series = MetricSeriesBuilder.Build(points);

        Assert.Equal(2.22, series[1].SpeedKmh!.Value, 0.15);
        // The first sample has no preceding interval, so it takes the first one's speed rather than a
        // fake zero that would dip the chart at the start.
        Assert.Equal(2.22, series[0].SpeedKmh!.Value, 0.15);
    }

    [Fact]
    public void Prefers_the_speed_the_device_recorded_over_the_derived_one()
    {
        // Geometry would give ≈ 2.22 km/h for this pair, so the recorded values can only come from
        // the source — the device reading wins where it exists.
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0, SpeedKmh: 25),
            new(47.51, 19.0, Time: T0.AddMinutes(30), SpeedKmh: 28),
        };

        var series = MetricSeriesBuilder.Build(points);

        Assert.Equal([25.0, 28.0], series.Select(s => s.SpeedKmh));
    }

    [Fact]
    public void Returns_empty_for_no_points()
    {
        Assert.Empty(MetricSeriesBuilder.Build([]));
    }

    [Fact]
    public void Passes_temperature_through_to_the_samples()
    {
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0, TemperatureCelsius: 8),
            new(47.51, 19.0, Time: T0.AddMinutes(30), TemperatureCelsius: 17),
        };

        var series = MetricSeriesBuilder.Build(points);

        Assert.Equal([8.0, 17.0], series.Select(s => s.TemperatureCelsius));
    }

    [Fact]
    public void Storable_series_is_kept_when_only_temperature_is_present()
    {
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0, TemperatureCelsius: 8),
            new(47.51, 19.0, Time: T0.AddMinutes(30), TemperatureCelsius: 17),
        };

        Assert.NotNull(MetricSeriesBuilder.BuildStorable(points));
    }

    [Fact]
    public void Merges_temperature_into_a_series_by_elapsed_time_fraction()
    {
        // Existing series (from a Polar GPX/TCX): no temperature, elapsed 0/5/10 min.
        var series = new List<MetricSample>
        {
            new(0, 0, 100, 120),
            new(1, 5, 110, 130),
            new(2, 10, 120, 140),
        };

        // Bryton FIT temperature points at 0/50%/100% of its own timeline: 10/15/20 °C.
        var fitPoints = new List<GeoPoint>
        {
            new(47.5, 19.0, Time: T0, TemperatureCelsius: 10),
            new(47.5, 19.0, Time: T0.AddMinutes(30), TemperatureCelsius: 15),
            new(47.5, 19.0, Time: T0.AddMinutes(60), TemperatureCelsius: 20),
        };

        var merged = MetricSeriesBuilder.MergeTemperature(series, fitPoints);

        // Fractions 0 / 0.5 / 1.0 line up: temperatures 10 / 15 / 20.
        Assert.Equal([10.0, 15.0, 20.0], merged.Select(s => s.TemperatureCelsius));
        // Other channels are untouched.
        Assert.Equal([100.0, 110.0, 120.0], merged.Select(s => s.ElevationMeters));
        Assert.Equal([120, 130, 140], merged.Select(s => s.HeartRate));
    }

    [Fact]
    public void Storable_series_is_kept_when_only_speed_can_be_derived()
    {
        // A plain GPX track: no elevation, heart rate or temperature — but position and time give a
        // speed graph, which is worth keeping.
        var points = new List<GeoPoint>
        {
            new(47.50, 19.0, Time: T0),
            new(47.51, 19.0, Time: T0.AddMinutes(30)),
        };

        Assert.NotNull(MetricSeriesBuilder.BuildStorable(points));
    }

    [Fact]
    public void Storable_series_is_null_when_there_is_nothing_to_graph()
    {
        // Without timestamps not even speed can be derived, so the series carries no channel at all.
        var barePoints = new List<GeoPoint>
        {
            new(47.50, 19.0),
            new(47.51, 19.0),
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

    [Fact]
    public void Leaves_the_speed_empty_where_the_reading_was_rejected()
    {
        // The graph and the ride's maximum read the same resolved speeds, so a sample the filter
        // rejects must be missing from the plotted series too — otherwise the line would show a
        // spike the headline number denies. `spanGaps` bridges it, so no hole appears.
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var points = Enumerable.Range(0, 10)
            .Select(i => new GeoPoint(0, i * 0.0000749, 100, start.AddSeconds(i), 140, null, i == 5 ? 85 : 30))
            .ToList();

        var series = MetricSeriesBuilder.Build(points);

        Assert.Null(series[5].SpeedKmh);
        Assert.Equal(30, series[4].SpeedKmh);
        Assert.Equal(30, series[6].SpeedKmh);
    }
}
