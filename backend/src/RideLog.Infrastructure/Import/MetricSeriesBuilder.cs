using RideLog.Application.Routes;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Builds the ride's downsampled metric series (for the elevation/HR graph) from its full track:
/// each sample carries cumulative distance and elapsed time plus the recorded elevation and heart
/// rate. Capped at <see cref="MaxSamples"/> points so a dense track stays a compact stored column.
/// </summary>
public static class MetricSeriesBuilder
{
    public const int MaxSamples = 500;

    public static IReadOnlyList<MetricSample> Build(IReadOnlyList<GeoPoint> points)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var startTime = points.FirstOrDefault(p => p.Time.HasValue).Time;

        var samples = new List<MetricSample>(points.Count);
        var cumulativeMeters = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                cumulativeMeters += GeoMath.DistanceMeters(points[i - 1], points[i]);
            }

            var elapsed = startTime is { } start && points[i].Time is { } t ? (t - start).TotalMinutes : 0;
            samples.Add(new MetricSample(
                Math.Round(cumulativeMeters / 1000.0, 3),
                Math.Round(elapsed, 3),
                points[i].ElevationMeters,
                points[i].HeartRate,
                points[i].TemperatureCelsius));
        }

        return Downsample(samples);
    }

    /// <summary>
    /// Builds the series for storage, returning null when it carries none of elevation, heart rate
    /// or temperature — such a series has nothing to graph, so there's no point keeping it.
    /// </summary>
    public static IReadOnlyList<MetricSample>? BuildStorable(IReadOnlyList<GeoPoint> points)
    {
        var series = Build(points);
        return series.Any(s => s.ElevationMeters is not null || s.HeartRate is not null || s.TemperatureCelsius is not null)
            ? series
            : null;
    }

    /// <summary>
    /// Fills the temperature channel of an existing series from a Bryton FIT's temperature track,
    /// aligning each sample to the FIT reading at the same fraction of elapsed time. The two sources
    /// sample at different points, so fractional alignment tolerates differing counts and a small
    /// start offset. Returns the series unchanged when the FIT carries no timed temperature.
    /// </summary>
    public static IReadOnlyList<MetricSample> MergeTemperature(
        IReadOnlyList<MetricSample> series, IReadOnlyList<GeoPoint> temperaturePoints)
    {
        var readings = temperaturePoints
            .Where(p => p is { TemperatureCelsius: not null, Time: not null })
            .OrderBy(p => p.Time!.Value)
            .ToList();
        if (readings.Count == 0 || series.Count == 0)
        {
            return series;
        }

        var fitStart = readings[0].Time!.Value;
        var fitSpan = (readings[^1].Time!.Value - fitStart).TotalSeconds;
        var seriesSpan = series[^1].ElapsedMinutes - series[0].ElapsedMinutes;

        return series
            .Select(sample =>
            {
                var fraction = seriesSpan > 0 ? (sample.ElapsedMinutes - series[0].ElapsedMinutes) / seriesSpan : 0;
                return sample with { TemperatureCelsius = TemperatureAtFraction(readings, fitStart, fitSpan, fraction) };
            })
            .ToList();
    }

    private static double TemperatureAtFraction(
        IReadOnlyList<GeoPoint> readings, DateTimeOffset fitStart, double fitSpanSeconds, double fraction)
    {
        var nearest = readings[0];
        var nearestGap = double.MaxValue;
        foreach (var reading in readings)
        {
            var readingFraction = fitSpanSeconds > 0 ? (reading.Time!.Value - fitStart).TotalSeconds / fitSpanSeconds : 0;
            var gap = Math.Abs(readingFraction - fraction);
            if (gap < nearestGap)
            {
                nearestGap = gap;
                nearest = reading;
            }
        }

        return nearest.TemperatureCelsius!.Value;
    }

    private static IReadOnlyList<MetricSample> Downsample(List<MetricSample> samples)
    {
        if (samples.Count <= MaxSamples)
        {
            return samples;
        }

        var stride = (int)Math.Ceiling((double)samples.Count / MaxSamples);
        var sampled = new List<MetricSample>();
        for (var i = 0; i < samples.Count; i += stride)
        {
            sampled.Add(samples[i]);
        }

        // Always keep the final sample so the series ends where the ride did.
        if (!sampled[^1].Equals(samples[^1]))
        {
            sampled.Add(samples[^1]);
        }

        return sampled;
    }
}
