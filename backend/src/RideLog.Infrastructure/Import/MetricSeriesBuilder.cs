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
                points[i].HeartRate));
        }

        return Downsample(samples);
    }

    /// <summary>
    /// Builds the series for storage, returning null when it carries neither elevation nor heart
    /// rate — such a series has nothing to graph, so there's no point keeping it.
    /// </summary>
    public static IReadOnlyList<MetricSample>? BuildStorable(IReadOnlyList<GeoPoint> points)
    {
        var series = Build(points);
        return series.Any(s => s.ElevationMeters is not null || s.HeartRate is not null) ? series : null;
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
