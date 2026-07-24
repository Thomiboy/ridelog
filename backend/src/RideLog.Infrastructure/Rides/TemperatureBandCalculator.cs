using RideLog.Application.Rides;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Buckets a ride's distance into fixed 5°C temperature bands (below 0, 0–5, …, 20–25, 25+) from
/// its per-point temperature series: each segment's distance is credited to the band of its
/// starting sample's temperature. Samples without a temperature are ignored.
/// </summary>
public static class TemperatureBandCalculator
{
    /// <summary>Inner band edges; the open-ended below-first and above-last bands wrap them.</summary>
    private static readonly int[] Edges = [0, 5, 10, 15, 20, 25];

    /// <summary>The seven bands as (from, to) bounds, from below-0 to 25+.</summary>
    public static readonly IReadOnlyList<(int? From, int? To)> Bands = BuildBands();

    public static IReadOnlyList<TemperatureBandSlice> KmPerBand(IReadOnlyList<MetricSample> series)
    {
        var km = new double[Bands.Count];
        for (var i = 0; i < series.Count - 1; i++)
        {
            if (series[i].TemperatureCelsius is not { } temperature)
            {
                continue;
            }

            var distance = series[i + 1].DistanceKm - series[i].DistanceKm;
            if (distance > 0)
            {
                km[BandIndex(temperature)] += distance;
            }
        }

        return Bands.Select((band, i) => new TemperatureBandSlice(band.From, band.To, km[i])).ToList();
    }

    private static int BandIndex(double temperature)
    {
        for (var i = 0; i < Edges.Length; i++)
        {
            if (temperature < Edges[i])
            {
                return i; // below this edge → the band ending at Edges[i]
            }
        }

        return Edges.Length; // at or above the last edge → the open-ended top band
    }

    private static IReadOnlyList<(int? From, int? To)> BuildBands()
    {
        var bands = new List<(int? From, int? To)> { (null, Edges[0]) };
        for (var i = 0; i < Edges.Length - 1; i++)
        {
            bands.Add((Edges[i], Edges[i + 1]));
        }

        bands.Add((Edges[^1], null));
        return bands;
    }
}
