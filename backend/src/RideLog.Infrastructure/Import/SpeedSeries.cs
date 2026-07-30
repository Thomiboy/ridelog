using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Resolves a track's per-point speed and rejects the readings a bicycle can't have produced.
/// Both the graph series and the ride's maximum read their speed from here, so the plotted line and
/// the headline number can never be computed down separate paths and disagree.
/// </summary>
/// <remarks>
/// Devices summarise a maximum speed of their own, but a single GPS jump lands in it and then owns
/// the top-speed record forever (docs/adr/0002). Resolving runs on the full track, before the series
/// is downsampled — the true peak sample is usually one of the ones downsampling drops.
/// </remarks>
public static class SpeedSeries
{
    /// <summary>
    /// The most a bicycle's speed can rise in a second (km/h). A strong sprint start is around
    /// 4 m/s² ≈ 14 km/h per second; anything beyond this is the track jumping, not the rider
    /// accelerating. Only the rise is bounded — braking is legitimately far more abrupt.
    /// </summary>
    private const double MaxRiseKmhPerSecond = 15.0;

    /// <summary>
    /// Each point's speed in km/h, or null where the track offers none or the reading was rejected.
    /// A rejected reading is left empty rather than replaced, exactly as if the device had never
    /// written one — the graph spans the gap and the maximum ignores it.
    /// </summary>
    public static IReadOnlyList<double?> Resolve(IReadOnlyList<GeoPoint> points)
    {
        var resolved = new double?[points.Count];
        double? lastAccepted = null;
        var lastAcceptedIndex = 0;

        for (var i = 0; i < points.Count; i++)
        {
            var speed = SpeedAt(points, i);
            if (speed is not { } kmh)
            {
                continue;
            }

            // The first reading has nothing before it to be judged against — a recording can start
            // with the rider already moving — so it sets the baseline. After that, each reading is
            // measured against the last *accepted* one: were it measured against the previous
            // sample, the first reading of a wider spike would become the baseline and the rest of
            // the spike would pass as a plausible continuation.
            if (lastAccepted is not { } baseline || IsPlausibleRise(points, lastAcceptedIndex, i, kmh - baseline))
            {
                resolved[i] = kmh;
                lastAccepted = kmh;
                lastAcceptedIndex = i;
            }
        }

        return resolved;
    }

    /// <summary>The fastest the track says the rider went, or null when it carries no usable speed.</summary>
    public static double? MaxKmh(IReadOnlyList<GeoPoint> points) =>
        Resolve(points).Where(s => s is not null).Select(s => s!.Value).DefaultIfEmpty().Max() is var max && max > 0
            ? max
            : null;

    /// <summary>
    /// Whether a rise of <paramref name="riseKmh"/> fits in the time since the last accepted
    /// reading. The bound scales with that interval, so a sparse track — or one where readings in
    /// between were rejected — is judged as leniently as its own resolution warrants; with no usable
    /// interval there's nothing to judge against, so the reading stands.
    /// </summary>
    private static bool IsPlausibleRise(IReadOnlyList<GeoPoint> points, int from, int to, double riseKmh)
    {
        if (riseKmh <= 0)
        {
            return true;
        }

        if (points[from].Time is not { } start || points[to].Time is not { } end)
        {
            return true;
        }

        var seconds = (end - start).TotalSeconds;
        return seconds <= 0 || riseKmh <= MaxRiseKmhPerSecond * seconds;
    }

    /// <summary>
    /// Speed at a point: the device's own reading when the source recorded one, otherwise derived
    /// from the distance and time to the previous point. The first point has no preceding interval,
    /// so it borrows the first one's speed rather than reading as a standstill.
    /// </summary>
    private static double? SpeedAt(IReadOnlyList<GeoPoint> points, int index)
    {
        if (points[index].SpeedKmh is { } recorded)
        {
            return Math.Round(recorded, 2);
        }

        var (from, to) = index == 0 ? (0, 1) : (index - 1, index);
        if (to >= points.Count || points[from].Time is not { } start || points[to].Time is not { } end)
        {
            return null;
        }

        var hours = (end - start).TotalHours;
        if (hours <= 0)
        {
            return null;
        }

        var km = GeoMath.DistanceMeters(points[from], points[to]) / 1000.0;
        return Math.Round(km / hours, 2);
    }
}
