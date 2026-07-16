using RideLog.Domain.Rides;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Time-overlap matching contract: the Bryton FIT merge (phase 2) uses this to find
/// the Polar ride a manually uploaded file belongs to, instead of creating a duplicate.
/// </summary>
public class RideOverlapTests
{
    private static Ride RideAt(string userId, DateTimeOffset start, DateTimeOffset end) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        EndTime = end,
        DistanceMeters = 1000,
        Duration = end - start,
        Sport = "ROAD_CYCLING",
        Source = RideSource.Polar,
    };

    private static readonly DateTimeOffset T0 = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rides_sharing_any_time_window_overlap()
    {
        var polar = RideAt("user-1", T0, T0.AddHours(2));
        var bryton = RideAt("user-1", T0.AddMinutes(5), T0.AddMinutes(115));

        Assert.True(polar.Overlaps(bryton));
        Assert.True(bryton.Overlaps(polar));
    }

    [Fact]
    public void Back_to_back_rides_do_not_overlap()
    {
        var morning = RideAt("user-1", T0, T0.AddHours(1));
        var afternoon = RideAt("user-1", T0.AddHours(1), T0.AddHours(2));

        Assert.False(morning.Overlaps(afternoon));
        Assert.False(afternoon.Overlaps(morning));
    }

    [Fact]
    public void Rides_of_different_users_never_overlap()
    {
        var mine = RideAt("user-1", T0, T0.AddHours(2));
        var theirs = RideAt("user-2", T0, T0.AddHours(2));

        Assert.False(mine.Overlaps(theirs));
    }
}
