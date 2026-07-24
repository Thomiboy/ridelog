using Dynastream.Fit;
using RideLog.Application.Import;
using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Parses Bryton (and other Garmin-FIT) activity files with the official FIT SDK. Bryton's value to
/// RideLog is the ambient temperature series, so temperature is summarised (avg/min/max) from the
/// record messages; the remaining metrics come from the Session summary, falling back to values
/// derived from the record track when the session omits them.
/// </summary>
internal sealed class FitActivityParser : IActivityFileParser
{
    // FIT positions are stored in semicircles: degrees = semicircles * 180 / 2^31.
    private const double SemicircleToDegrees = 180.0 / 2147483648.0;

    public bool CanParse(string fileName) =>
        fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase);

    public ParsedActivity Parse(Stream content, string fileName)
    {
        var records = new List<RecordMesg>();
        SessionMesg? session = null;

        var decode = new Decode();
        var broadcaster = new MesgBroadcaster();
        decode.MesgEvent += broadcaster.OnMesg;
        broadcaster.RecordMesgEvent += (_, e) => records.Add((RecordMesg)e.mesg);
        broadcaster.SessionMesgEvent += (_, e) => session ??= (SessionMesg)e.mesg;

        // Read from the start; callers may hand us a stream the integrity probe already advanced.
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        if (!decode.Read(content) || records.Count == 0)
        {
            throw new FormatException($"FIT file '{fileName}' contains no record data.");
        }

        var points = records
            .Select(ToGeoPoint)
            .Where(p => p is not null)
            .Select(p => p!.Value)
            .ToList();

        var timestamps = records
            .Select(r => r.GetTimestamp())
            .Where(t => t is not null)
            .Select(t => ToDateTimeOffset(t))
            .OrderBy(t => t)
            .ToList();
        if (timestamps.Count == 0)
        {
            throw new FormatException($"FIT file '{fileName}' has no record timestamps.");
        }

        var start = timestamps[0];
        var end = timestamps[^1];
        var duration = session?.GetTotalTimerTime() is { } timer
            ? TimeSpan.FromSeconds(timer)
            : end - start;

        var distance = session?.GetTotalDistance() ?? ComputedDistance(points);
        var elevationGain = session?.GetTotalAscent() is { } ascent ? ascent : ComputedElevationGain(points);

        var temperatures = records
            .Select(r => r.GetTemperature())
            .Where(t => t is not null)
            .Select(t => (double)t!.Value)
            .ToList();

        return new ParsedActivity
        {
            StartTime = start,
            EndTime = end,
            Duration = duration,
            DistanceMeters = distance,
            Sport = session?.GetSport()?.ToString() ?? "Cycling",
            AverageSpeedKmh = session?.GetAvgSpeed() is { } avg ? avg * 3.6 : SpeedFrom(distance, duration),
            MaximumSpeedKmh = session?.GetMaxSpeed() is { } max ? max * 3.6 : null,
            AverageHeartRate = session?.GetAvgHeartRate(),
            MaximumHeartRate = session?.GetMaxHeartRate(),
            ElevationGainMeters = elevationGain,
            AverageCadence = session?.GetAvgCadence(),
            Calories = session?.GetTotalCalories(),
            AverageTemperatureCelsius = temperatures.Count > 0 ? temperatures.Average() : null,
            MinTemperatureCelsius = temperatures.Count > 0 ? temperatures.Min() : null,
            MaxTemperatureCelsius = temperatures.Count > 0 ? temperatures.Max() : null,
            RoutePoints = points,
        };
    }

    private static GeoPoint? ToGeoPoint(RecordMesg record)
    {
        var lat = record.GetPositionLat();
        var lon = record.GetPositionLong();
        if (lat is null || lon is null)
        {
            return null;
        }

        var time = record.GetTimestamp();
        return new GeoPoint(
            lat.Value * SemicircleToDegrees,
            lon.Value * SemicircleToDegrees,
            record.GetAltitude(),
            time is null ? null : ToDateTimeOffset(time));
    }

    private static DateTimeOffset ToDateTimeOffset(Dynastream.Fit.DateTime time) =>
        new(System.DateTime.SpecifyKind(time.GetDateTime(), DateTimeKind.Utc));

    private static double ComputedDistance(IReadOnlyList<GeoPoint> points)
    {
        var distance = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            distance += GeoMath.DistanceMeters(points[i - 1], points[i]);
        }

        return distance;
    }

    private static double? ComputedElevationGain(IReadOnlyList<GeoPoint> points)
    {
        if (!points.Any(p => p.ElevationMeters.HasValue))
        {
            return null;
        }

        var gain = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].ElevationMeters is { } current && points[i - 1].ElevationMeters is { } previous && current > previous)
            {
                gain += current - previous;
            }
        }

        return gain;
    }

    private static double? SpeedFrom(double distanceMeters, TimeSpan duration) =>
        duration > TimeSpan.Zero ? distanceMeters / 1000 / duration.TotalHours : null;
}
