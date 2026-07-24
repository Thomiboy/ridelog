using RideLog.Application.Rides;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Computes time-in-zone from a ride's stored HR series against the classic five-zone model, with
/// the zone floors fixed at 50/60/70/80/90% of the configured maximum heart rate. Each interval
/// between two samples is credited to the zone of its starting sample; readings below Z1 (warm-up)
/// and samples without a heart rate are ignored.
/// </summary>
public static class HrZoneCalculator
{
    /// <summary>Zone floor as a fraction of max HR: Z1 ≥50%, Z2 ≥60%, … Z5 ≥90%.</summary>
    public static readonly IReadOnlyList<double> ZoneFloorPercents = [0.50, 0.60, 0.70, 0.80, 0.90];

    public const int ZoneCount = 5;

    public static IReadOnlyList<HrZoneSlice> TimeInZone(IReadOnlyList<MetricSample> series, int maxHeartRate)
    {
        var floors = ZoneFloorPercents.Select(p => p * maxHeartRate).ToArray();
        var minutes = new double[ZoneCount];

        for (var i = 0; i < series.Count - 1; i++)
        {
            var sample = series[i];
            if (sample.HeartRate is not { } hr)
            {
                continue;
            }

            var zone = ZoneFor(hr, floors);
            if (zone is null)
            {
                continue;
            }

            var interval = series[i + 1].ElapsedMinutes - sample.ElapsedMinutes;
            if (interval > 0)
            {
                minutes[zone.Value - 1] += interval;
            }
        }

        return Enumerable.Range(1, ZoneCount)
            .Select(zone => new HrZoneSlice(zone, minutes[zone - 1]))
            .ToList();
    }

    private static int? ZoneFor(int heartRate, IReadOnlyList<double> floors)
    {
        if (heartRate < floors[0])
        {
            return null; // below Z1 — warm-up / recovery, not counted
        }

        for (var zone = ZoneCount; zone >= 1; zone--)
        {
            if (heartRate >= floors[zone - 1])
            {
                return zone;
            }
        }

        return null;
    }
}
