using System.Globalization;
using System.Xml.Linq;
using RideLog.Application.Import;
using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Import;

/// <summary>
/// Parses Garmin TCX activities. TCX carries HR, cadence and its own per-lap distance, so those
/// come straight from the file rather than being derived. Element lookups are namespace-agnostic.
/// </summary>
internal sealed class TcxActivityParser : IActivityFileParser
{
    public bool CanParse(string fileName) =>
        fileName.EndsWith(".tcx", StringComparison.OrdinalIgnoreCase);

    public ParsedActivity Parse(Stream content, string fileName)
    {
        var doc = XDocument.Load(content);

        var activity = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Activity")
            ?? throw new FormatException($"TCX file '{fileName}' contains no activity.");

        var trackpoints = doc.Descendants().Where(e => e.Name.LocalName == "Trackpoint").ToList();
        if (trackpoints.Count == 0)
        {
            throw new FormatException($"TCX file '{fileName}' contains no track points.");
        }

        var times = trackpoints
            .Select(tp => Child(tp, "Time"))
            .Where(t => t is not null)
            .Select(t => DateTimeOffset.Parse(t!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
            .ToList();
        var start = times.Min();
        var end = times.Max();

        var points = trackpoints.Select(ToGeoPoint).Where(p => p is not null).Select(p => p!.Value).ToList();

        var heartRates = trackpoints
            .Select(tp => Descendant(tp, "HeartRateBpm"))
            .Where(hr => hr is not null)
            .Select(hr => int.Parse(Child(hr!, "Value")!, CultureInfo.InvariantCulture))
            .ToList();

        var cadences = trackpoints
            .Select(tp => Child(tp, "Cadence"))
            .Where(c => c is not null)
            .Select(c => int.Parse(c!, CultureInfo.InvariantCulture))
            .ToList();

        var distance = doc.Descendants()
            .Where(e => e.Name.LocalName == "Lap")
            .Sum(lap => double.Parse(Child(lap, "DistanceMeters") ?? "0", CultureInfo.InvariantCulture));

        var elevationGain = 0.0;
        var hasElevation = points.Any(p => p.ElevationMeters.HasValue);
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].ElevationMeters is { } current && points[i - 1].ElevationMeters is { } previous && current > previous)
            {
                elevationGain += current - previous;
            }
        }

        var duration = end - start;

        return new ParsedActivity
        {
            StartTime = start,
            EndTime = end,
            Duration = duration,
            DistanceMeters = distance,
            Sport = activity.Attribute("Sport")?.Value is { Length: > 0 } sport ? sport : "Unknown",
            AverageHeartRate = heartRates.Count > 0 ? (int)Math.Round(heartRates.Average()) : null,
            MaximumHeartRate = heartRates.Count > 0 ? heartRates.Max() : null,
            AverageCadence = cadences.Count > 0 ? (int)Math.Round(cadences.Average()) : null,
            ElevationGainMeters = hasElevation ? elevationGain : null,
            AverageSpeedKmh = duration > TimeSpan.Zero ? distance / 1000 / duration.TotalHours : null,
            RoutePoints = points,
        };
    }

    private static GeoPoint? ToGeoPoint(XElement trackpoint)
    {
        var position = Descendant(trackpoint, "Position");
        if (position is null)
        {
            return null;
        }

        var lat = double.Parse(Child(position, "LatitudeDegrees")!, CultureInfo.InvariantCulture);
        var lon = double.Parse(Child(position, "LongitudeDegrees")!, CultureInfo.InvariantCulture);
        var ele = Child(trackpoint, "AltitudeMeters");
        var time = Child(trackpoint, "Time");

        return new GeoPoint(
            lat,
            lon,
            ele is null ? null : double.Parse(ele, CultureInfo.InvariantCulture),
            time is null ? null : DateTimeOffset.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    private static string? Child(XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static XElement? Descendant(XElement element, string localName) =>
        element.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
}
