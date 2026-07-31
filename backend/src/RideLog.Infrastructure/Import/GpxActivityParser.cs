using System.Globalization;
using System.Xml.Linq;
using RideLog.Application.Import;
using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Parses GPX tracks. GPX carries the route and elevation but no HR/cadence, so distance and
/// elevation gain are derived from the track points. Element lookups are namespace-agnostic to
/// tolerate GPX 1.0/1.1 and vendor variations.
/// </summary>
internal sealed class GpxActivityParser : IActivityFileParser
{
    public bool CanParse(string fileName) =>
        fileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase);

    public ParsedActivity Parse(Stream content, string fileName)
    {
        var doc = XDocument.Load(content);

        var points = doc.Descendants()
            .Where(e => e.Name.LocalName == "trkpt")
            .Select(ToGeoPoint)
            .ToList();

        if (points.Count == 0)
        {
            throw new FormatException($"GPX file '{fileName}' contains no track points.");
        }

        var times = points.Where(p => p.Time.HasValue).Select(p => p.Time!.Value).ToList();
        var start = times.Count > 0 ? times.Min() : throw new FormatException($"GPX file '{fileName}' has no timestamps.");
        var end = times.Max();

        var distance = 0.0;
        var elevationGain = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            distance += GeoMath.DistanceMeters(points[i - 1], points[i]);
            if (points[i].ElevationMeters is { } current && points[i - 1].ElevationMeters is { } previous && current > previous)
            {
                elevationGain += current - previous;
            }
        }

        var duration = end - start;
        var sport = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;

        return new ParsedActivity
        {
            StartTime = start,
            EndTime = end,
            Duration = duration,
            DistanceMeters = distance,
            Sport = string.IsNullOrWhiteSpace(sport) ? "Unknown" : sport,
            ElevationGainMeters = points.Any(p => p.ElevationMeters.HasValue) ? elevationGain : null,
            AverageSpeedKmh = duration > TimeSpan.Zero ? distance / 1000 / duration.TotalHours : null,
            RoutePoints = points,
        };
    }

    private static GeoPoint ToGeoPoint(XElement trkpt)
    {
        var lat = double.Parse(trkpt.Attribute("lat")!.Value, CultureInfo.InvariantCulture);
        var lon = double.Parse(trkpt.Attribute("lon")!.Value, CultureInfo.InvariantCulture);
        var eleText = trkpt.Elements().FirstOrDefault(e => e.Name.LocalName == "ele")?.Value;
        var timeText = trkpt.Elements().FirstOrDefault(e => e.Name.LocalName == "time")?.Value;

        return new GeoPoint(
            lat,
            lon,
            eleText is null ? null : double.Parse(eleText, CultureInfo.InvariantCulture),
            timeText is null ? null : DateTimeOffset.Parse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}
