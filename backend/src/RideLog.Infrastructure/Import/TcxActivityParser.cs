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

        var laps = doc.Descendants().Where(e => e.Name.LocalName == "Lap").ToList();

        var distance = laps.Sum(lap => double.Parse(Child(lap, "DistanceMeters") ?? "0", CultureInfo.InvariantCulture));

        // Lap MaximumSpeed is metres per second; take the fastest lap and convert to km/h.
        var lapMaxSpeeds = laps
            .Select(lap => Child(lap, "MaximumSpeed"))
            .Where(s => s is not null)
            .Select(s => double.Parse(s!, CultureInfo.InvariantCulture))
            .ToList();
        var maximumSpeedKmh = lapMaxSpeeds.Count > 0 ? lapMaxSpeeds.Max() * 3.6 : (double?)null;

        var lapCalories = laps
            .Select(lap => Child(lap, "Calories"))
            .Where(c => c is not null)
            .Select(c => int.Parse(c!, CultureInfo.InvariantCulture))
            .ToList();
        var calories = lapCalories.Count > 0 ? lapCalories.Sum() : (int?)null;

        var averageSpeedKmh = AverageSpeedKmh(laps, distance, end - start);

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
            MaximumSpeedKmh = maximumSpeedKmh,
            Calories = calories,
            AverageSpeedKmh = averageSpeedKmh,
            RoutePoints = points,
        };
    }

    /// <summary>
    /// Average speed with source-first precedence: Polar records its own moving-time average in the
    /// Lap LX extension (m/s), so prefer that (distance-weighted across laps). Failing that, derive
    /// from distance and the summed Lap moving time. Elapsed wall time is only the last resort — it
    /// reads low whenever the rider stopped.
    /// </summary>
    private static double? AverageSpeedKmh(IReadOnlyList<XElement> laps, double distanceMeters, TimeSpan elapsed)
    {
        var weighted = laps
            .Select(lap => new
            {
                Distance = double.Parse(Child(lap, "DistanceMeters") ?? "0", CultureInfo.InvariantCulture),
                AvgSpeed = Descendant(lap, "AvgSpeed") is { } s ? double.Parse(s.Value, CultureInfo.InvariantCulture) : (double?)null,
            })
            .Where(l => l.AvgSpeed is not null && l.Distance > 0)
            .ToList();
        if (weighted.Count > 0)
        {
            var totalDistance = weighted.Sum(l => l.Distance);
            return weighted.Sum(l => l.AvgSpeed!.Value * l.Distance) / totalDistance * 3.6;
        }

        var movingSeconds = laps
            .Select(lap => Child(lap, "TotalTimeSeconds"))
            .Where(t => t is not null)
            .Sum(t => double.Parse(t!, CultureInfo.InvariantCulture));
        if (movingSeconds > 0)
        {
            return distanceMeters / 1000 / (movingSeconds / 3600);
        }

        return elapsed > TimeSpan.Zero ? distanceMeters / 1000 / elapsed.TotalHours : null;
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
        var hrElement = Descendant(trackpoint, "HeartRateBpm");
        var hr = hrElement is null ? null : Child(hrElement, "Value");

        return new GeoPoint(
            lat,
            lon,
            ele is null ? null : double.Parse(ele, CultureInfo.InvariantCulture),
            time is null ? null : DateTimeOffset.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            hr is null ? null : (int)Math.Round(double.Parse(hr, CultureInfo.InvariantCulture)));
    }

    private static string? Child(XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static XElement? Descendant(XElement element, string localName) =>
        element.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
}
