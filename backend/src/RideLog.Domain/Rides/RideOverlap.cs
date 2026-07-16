namespace RideLog.Domain.Rides;

/// <summary>
/// The time-overlap rule shared by ride deduplication and the phase-2 Bryton merge:
/// two recording windows intersect when each starts before the other ends. Touching
/// boundaries (back-to-back rides) do not overlap.
/// </summary>
public static class RideOverlap
{
    public static bool Intersects(
        DateTimeOffset startA, DateTimeOffset endA,
        DateTimeOffset startB, DateTimeOffset endB)
        => startA < endB && startB < endA;
}
