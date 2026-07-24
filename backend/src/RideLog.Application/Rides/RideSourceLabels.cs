using RideLog.Domain.Rides;

namespace RideLog.Application.Rides;

/// <summary>
/// Derives the source chips shown on a ride from its origin and attached raw files. Tokens are
/// stable and language-neutral; the frontend maps them to localized labels
/// (Polar · Auto-sync, Polar · Import, Bryton).
/// </summary>
public static class RideSourceLabels
{
    public const string PolarAutoSync = "PolarAutoSync";
    public const string PolarImport = "PolarImport";
    public const string Bryton = "Bryton";

    public static IReadOnlyList<string> Derive(RideSource source, IEnumerable<RawFileFormat> rawFormats)
    {
        var formats = rawFormats as IReadOnlyCollection<RawFileFormat> ?? rawFormats.ToList();
        var labels = new List<string>();

        if (source == RideSource.Polar)
        {
            labels.Add(PolarAutoSync);
        }
        else if (formats.Any(f => f is RawFileFormat.Gpx or RawFileFormat.Tcx))
        {
            // A manual GPX/TCX bulk import originally came from Polar Flow.
            labels.Add(PolarImport);
        }

        if (formats.Contains(RawFileFormat.Fit))
        {
            labels.Add(Bryton);
        }

        return labels;
    }
}
