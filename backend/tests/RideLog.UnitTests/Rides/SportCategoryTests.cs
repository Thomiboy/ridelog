using RideLog.Application.Rides;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// The raw strings here are what the sources actually write: Polar shouts in upper snake case, TCX
/// capitalises, GPX is lower case, and a file with no sport at all is stored as "Unknown".
/// </summary>
public sealed class SportCategoryTests
{
    [Theory]
    [InlineData("ROAD_BIKING", SportCategory.Cycling)]   // Polar
    [InlineData("Biking", SportCategory.Cycling)]        // TCX
    [InlineData("cycling", SportCategory.Cycling)]       // GPX
    [InlineData("MOUNTAIN_BIKING", SportCategory.Cycling)]
    [InlineData("RUNNING", SportCategory.Running)]
    [InlineData("Jogging", SportCategory.Running)]
    [InlineData("WALKING", SportCategory.Walking)]
    [InlineData("HIKING", SportCategory.Hiking)]
    [InlineData("POOL_SWIMMING", SportCategory.Swimming)]
    [InlineData("ROWING", SportCategory.Rowing)]
    [InlineData("CROSS-COUNTRY_SKIING", SportCategory.Skiing)]
    [InlineData("INLINE_SKATING", SportCategory.Skating)]
    [InlineData("STRENGTH_TRAINING", SportCategory.Other)]
    [InlineData("YOGA", SportCategory.Other)]
    public void Reads_the_category_out_of_whatever_the_source_wrote(string sport, SportCategory expected)
    {
        Assert.Equal(expected, SportCategories.Of(sport));
    }

    /// <summary>
    /// A name we don't recognise counts as cycling, because this is a cycling log and its untagged
    /// history — the one-time bulk import — is rides. Recognising non-cycling rather than
    /// whitelisting cycling is what lets that history through.
    /// </summary>
    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("VIRTUAL_RIDE")]
    public void Counts_a_name_it_does_not_recognise_as_cycling(string sport)
    {
        Assert.Equal(SportCategory.Cycling, SportCategories.Of(sport));
    }
}
