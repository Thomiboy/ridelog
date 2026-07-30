using RideLog.Application.Routes;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Rest detection as it actually runs in production: the stored series is the *downsampled* one, so
/// the detector must find a stop in that, not in a hand-made four-point series.
/// </summary>
public class RestStopRealisticTrackTests
{
    /// <summary>
    /// A 1 Hz ride heading east along the equator at ~25 km/h, with one genuine stop of
    /// <paramref name="restMinutes"/> minutes at the halfway mark.
    /// </summary>
    private static List<GeoPoint> TrackWithRest(int ridingMinutes, int restMinutes)
    {
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        // ~25 km/h ≈ 6.94 m/s; one degree of longitude on the equator is ~111.195 km.
        const double metresPerSecond = 6.94;
        var degreesPerSecond = metresPerSecond / (6_371_000 * Math.PI / 180);

        var points = new List<GeoPoint>();
        var seconds = 0;
        var longitude = 0.0;
        var half = ridingMinutes * 60 / 2;

        for (var i = 0; i < half; i++)
        {
            points.Add(new GeoPoint(0, longitude, 100, start.AddSeconds(seconds), 140));
            longitude += degreesPerSecond;
            seconds++;
        }

        // Stopped: the clock runs, the position does not.
        for (var i = 0; i < restMinutes * 60; i++)
        {
            points.Add(new GeoPoint(0, longitude, 100, start.AddSeconds(seconds), 90));
            seconds++;
        }

        for (var i = 0; i < ridingMinutes * 60 - half; i++)
        {
            points.Add(new GeoPoint(0, longitude, 100, start.AddSeconds(seconds), 140));
            longitude += degreesPerSecond;
            seconds++;
        }

        return points;
    }

    /// <summary>Standing still for <paramref name="seconds"/> before or after the ride's riding part.</summary>
    private static List<GeoPoint> Idling(List<GeoPoint> track, int seconds, bool atStart)
    {
        var anchor = atStart ? track[0] : track[^1];
        var idle = Enumerable.Range(0, seconds)
            .Select(i => anchor with { Time = anchor.Time!.Value.AddSeconds(atStart ? i - seconds : i + 1) })
            .ToList();
        return atStart ? [.. idle, .. track] : [.. track, .. idle];
    }

    [Fact]
    public void Finds_a_ten_minute_stop_on_a_normal_length_ride()
    {
        // Two hours of riding with a ten-minute coffee stop in the middle — an unmissable rest.
        var track = TrackWithRest(ridingMinutes: 120, restMinutes: 10);
        var series = MetricSeriesBuilder.Build(track);

        var rests = RestStopDetector.RestStops(series, track);

        // One stop, and it sits where the rider actually stood: the ride is symmetric around the
        // stop, so that's the equator at half the total longitude covered.
        var rest = Assert.Single(rests);
        Assert.Equal(0, rest.Latitude, 4);
        Assert.Equal(track[^1].Longitude / 2, rest.Longitude, 2);
    }

    [Fact]
    public void Finds_the_same_stop_regardless_of_how_long_the_ride_is()
    {
        // The stored series is always ≤500 samples, so a longer ride spaces them further apart.
        // The same ten-minute stop must be found either way — detection can't depend on the stride.
        var shortRide = MetricSeriesBuilder.Build(TrackWithRest(ridingMinutes: 40, restMinutes: 10));
        var longRide = MetricSeriesBuilder.Build(TrackWithRest(ridingMinutes: 540, restMinutes: 10));

        Assert.Single(RestStopDetector.DetectRestDistancesKm(shortRide));
        Assert.Single(RestStopDetector.DetectRestDistancesKm(longRide));
    }

    [Fact]
    public void Does_not_invent_a_stop_on_a_ride_that_never_paused()
    {
        var track = TrackWithRest(ridingMinutes: 120, restMinutes: 0);

        Assert.Empty(RestStopDetector.DetectRestDistancesKm(MetricSeriesBuilder.Build(track)));
    }

    [Fact]
    public void Ignores_standing_still_before_setting_off()
    {
        // Waiting for a GPS fix isn't a pause *within* the ride, and a marker there would land under
        // the start dot anyway.
        var track = Idling(TrackWithRest(ridingMinutes: 120, restMinutes: 0), seconds: 300, atStart: true);

        Assert.Empty(RestStopDetector.DetectRestDistancesKm(MetricSeriesBuilder.Build(track)));
    }

    [Fact]
    public void Ignores_a_recording_left_running_after_the_ride()
    {
        var track = Idling(TrackWithRest(ridingMinutes: 120, restMinutes: 0), seconds: 300, atStart: false);

        Assert.Empty(RestStopDetector.DetectRestDistancesKm(MetricSeriesBuilder.Build(track)));
    }
}
