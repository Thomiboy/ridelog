namespace RideLog.Application.Rides;

/// <summary>
/// What kind of activity a recording is, once the source's own wording has been read. Derived on
/// read and never stored, so a wrong reading is corrected by shipping code rather than by
/// reprocessing every ride (docs/adr/0004).
/// </summary>
public enum SportCategory
{
    /// <summary>A ride. Also where anything unrecognised lands — see <see cref="SportCategories"/>.</summary>
    Cycling,
    Running,
    Walking,
    Hiking,
    Swimming,
    Rowing,
    Skiing,
    Skating,

    /// <summary>Recognisably not cycling, but not one of the kinds worth naming — strength work, yoga.</summary>
    Other,
}

/// <summary>
/// Reads a source's own wording for a sport. Sources disagree about spelling and case — Polar shouts
/// "ROAD_BIKING", TCX writes "Biking", GPX writes "cycling" — so this matches on the fragment they
/// have in common.
///
/// It recognises what is *not* cycling rather than listing what is. That is the whole trick: the
/// historical bulk import carries no sport at all, and a whitelist would have hidden every ride in
/// it. Anything unrecognised is therefore a ride.
/// </summary>
public static class SportCategories
{
    private static readonly (string Fragment, SportCategory Category)[] NotCycling =
    [
        ("run", SportCategory.Running),
        ("jog", SportCategory.Running),
        ("walk", SportCategory.Walking),
        ("hik", SportCategory.Hiking),
        ("swim", SportCategory.Swimming),
        ("row", SportCategory.Rowing),
        ("ski", SportCategory.Skiing),
        ("skat", SportCategory.Skating),
        ("strength", SportCategory.Other),
        ("yoga", SportCategory.Other),
        ("elliptical", SportCategory.Other),
    ];

    /// <summary>
    /// The fragments that mark a recording as not cycling, for callers that have to express the same
    /// question as a database filter rather than a function.
    /// </summary>
    public static IReadOnlyList<string> NotCyclingFragments { get; } =
        NotCycling.Select(entry => entry.Fragment).ToList();

    public static SportCategory Of(string? sport)
    {
        var name = sport?.ToLowerInvariant() ?? string.Empty;

        foreach (var (fragment, category) in NotCycling)
        {
            if (name.Contains(fragment, StringComparison.Ordinal))
            {
                return category;
            }
        }

        return SportCategory.Cycling;
    }
}
