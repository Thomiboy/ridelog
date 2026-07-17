namespace RideLog.Application.Rides;

/// <summary>
/// The UI shows cycling only, but sport is stored raw and varies by source (Polar "ROAD_BIKING",
/// TCX "Biking", GPX "cycling" or missing → "Unknown"). Rather than whitelist every cycling
/// variant, we exclude known non-cycling sports — so untagged historical imports still show up.
/// </summary>
public static class CyclingRides
{
    public static readonly IReadOnlyList<string> NonCyclingKeywords =
        ["run", "jog", "walk", "hik", "swim", "row", "ski", "skat", "strength", "yoga", "elliptical"];
}
