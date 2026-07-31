using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Resolves a track's per-point speed and rejects the readings a bicycle can't have produced.
/// Both the graph series and the ride's maximum read their speed from here, so the plotted line and
/// the headline number can never be computed down separate paths and disagree.
/// </summary>
/// <remarks>
/// Resolving runs on the full track, before the series is downsampled — the true peak sample is
/// usually one of the ones downsampling drops. What comes out feeds the graph, and checks the
/// device's own summary rather than replacing it: on real rides this device's positions are far
/// noisier than its speed sensor (docs/adr/0003).
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
    /// The most a bicycle's speed can fall in a second (km/h). Braking is far more abrupt than
    /// accelerating — this is around the limit of tyre grip — so it only rules out drops no brake
    /// produces. Used solely to judge a track's opening reading, which has nothing before it.
    /// </summary>
    private const double MaxFallKmhPerSecond = 30.0;

    /// <summary>
    /// The longest interval either bound is allowed to earn room over (seconds). Acceleration decays
    /// within a couple of seconds as drag takes over, so a rider cannot bank a bigger jump simply by
    /// being sampled less often. Without this the bounds scale away to nothing on a sparse track —
    /// at one sample per ten seconds a rise of 150 km/h counts as plausible.
    /// </summary>
    private const double MaxJudgedSeconds = 2.0;

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
        int? provisionalIndex = null;

        for (var i = 0; i < points.Count; i++)
        {
            var speed = SpeedAt(points, i);
            if (speed is not { } kmh)
            {
                continue;
            }

            if (lastAccepted is not { } baseline)
            {
                // A recording can start with the rider already moving, so the opening reading has to
                // be allowed to be any speed. It's held provisionally: nothing precedes it to judge
                // it against, but the reading that follows can still expose it (see below).
                resolved[i] = kmh;
                lastAccepted = kmh;
                lastAcceptedIndex = i;
                provisionalIndex = i;
                continue;
            }

            var change = kmh - baseline;

            // While the baseline is still an unproven opening reading, a drop no brake could have
            // produced is what exposes that reading as the glitch.
            var condemned = provisionalIndex is { } opening && !IsPlausibleFall(points, opening, i, -change);
            if (condemned)
            {
                resolved[provisionalIndex!.Value] = null;
            }

            // Each reading is measured against the last *accepted* one. Were it measured against the
            // previous sample, the first reading of a wider spike would become the baseline and the
            // rest of the spike would pass as a plausible continuation.
            if (IsPlausibleRise(points, lastAcceptedIndex, i, change))
            {
                resolved[i] = kmh;
                lastAccepted = kmh;
                lastAcceptedIndex = i;
                // Replacing a condemned opening leaves this reading just as unproven as that one
                // was, so it inherits the provisional status — a glitch spanning two intervals
                // otherwise loses its first half and keeps its second.
                provisionalIndex = condemned ? i : null;
            }

            // A rejected reading is itself bogus, so it says nothing about an unproven opening one —
            // that stays pending until a reading we trust either confirms or condemns it. Clearing it
            // here let a 190 km/h opening survive: the reading after it was a wilder 770 km/h that
            // got rejected, and the opening was never looked at again.
        }

        return resolved;
    }

    /// <summary>
    /// How far above what the track supports a device's own summary may sit before the track
    /// overrules it. Five real rides put the honest summaries within a few per cent of their track
    /// and the one dishonest summary at nearly twice it, so the gap is wide and this sits in it.
    /// </summary>
    private const double DeviceVetoFactor = 1.5;

    /// <summary>
    /// The ride's top speed. The device's own summary leads — it measures speed far better than we
    /// can reconstruct it from GPS positions — and the track is only allowed to overrule a summary
    /// it cannot support at all (docs/adr/0003). With one source missing, the other stands alone.
    /// </summary>
    public static double? TopSpeedKmh(IReadOnlyList<GeoPoint> points, double? deviceMaximumKmh)
    {
        var fromTrack = MaxKmh(points);
        if (deviceMaximumKmh is not { } device)
        {
            return fromTrack;
        }

        return fromTrack is { } track && device > track * DeviceVetoFactor ? track : device;
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
    private static bool IsPlausibleRise(IReadOnlyList<GeoPoint> points, int from, int to, double riseKmh) =>
        FitsInTheInterval(points, from, to, riseKmh, MaxRiseKmhPerSecond);

    /// <summary>
    /// Whether a drop of <paramref name="fallKmh"/> is one a brake could have produced over the time
    /// between the two points. Only asked of a track's opening reading: everywhere else the rise
    /// bound already has a trustworthy baseline to work from.
    /// </summary>
    private static bool IsPlausibleFall(IReadOnlyList<GeoPoint> points, int from, int to, double fallKmh) =>
        FitsInTheInterval(points, from, to, fallKmh, MaxFallKmhPerSecond);

    private static bool FitsInTheInterval(
        IReadOnlyList<GeoPoint> points, int from, int to, double changeKmh, double maxKmhPerSecond)
    {
        if (changeKmh <= 0)
        {
            return true;
        }

        if (points[from].Time is not { } start || points[to].Time is not { } end)
        {
            return true;
        }

        var seconds = (end - start).TotalSeconds;
        return seconds <= 0 || changeKmh <= maxKmhPerSecond * Math.Min(seconds, MaxJudgedSeconds);
    }

    /// <summary>
    /// Speed at a point: the device's own reading when the source recorded one, otherwise derived
    /// from the distance and time to the previous point.
    /// </summary>
    /// <remarks>
    /// The first point has no preceding interval and so has no derived speed. It used to borrow the
    /// second point's, which reads better on a graph but made the opening pair of readings identical
    /// by construction — and a glitched first interval then produced two matching bogus readings
    /// that no rate-of-change rule can tell apart. An empty opening reading is honest, and the chart
    /// spans it.
    /// </remarks>
    private static double? SpeedAt(IReadOnlyList<GeoPoint> points, int index)
    {
        if (points[index].SpeedKmh is { } recorded)
        {
            return Math.Round(recorded, 2);
        }

        if (index == 0)
        {
            return null;
        }

        var (from, to) = (index - 1, index);
        if (points[from].Time is not { } start || points[to].Time is not { } end)
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
