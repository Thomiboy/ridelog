namespace RideLog.Application.Rides;

/// <summary>
/// The cycling filter the ride queries apply, expressed as the fragments to exclude so it can be
/// translated to SQL. Same knowledge as <see cref="SportCategories"/> and read from it, so the rides
/// list and the other-activities list can never disagree about where a recording belongs.
/// </summary>
public static class CyclingRides
{
    public static readonly IReadOnlyList<string> NonCyclingKeywords = SportCategories.NotCyclingFragments;
}
