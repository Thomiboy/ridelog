using RideLog.Application.Routes;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

public sealed class HeadwindCalculatorTests
{
    // Weather services report the direction wind blows *from*, so "wind at 0°" is a northerly:
    // it comes out of the north and pushes southward. Riding north therefore meets it head-on,
    // riding south is pushed along, and riding east or west is crossed by it.
    [Theory]
    [InlineData(0, 20)]    // riding north, straight into a northerly
    [InlineData(90, 0)]    // riding east, crossed by it
    [InlineData(180, -20)] // riding south, pushed along by it
    [InlineData(270, 0)]   // riding west, crossed the other way
    public void Resolves_the_wind_onto_the_direction_being_ridden(double ridingBearing, double expected)
    {
        var headwind = HeadwindCalculator.Component(
            ridingBearing,
            windFromBearing: 0,
            windSpeedKmh: 20);

        Assert.Equal(expected, headwind, 0.001);
    }

    // The bearing a stretch was ridden on comes from its end points. Due north is exact; due east
    // is within a thousandth of a degree of 90° for separations this short, because a great circle
    // between two points on the same parallel bows very slightly poleward.
    [Theory]
    [InlineData(47.51, 19.04, 0, 20)]    // rode north into a northerly
    [InlineData(47.50, 19.05, 90, 20)]   // rode east into an easterly
    [InlineData(47.49, 19.04, 0, -20)]   // rode south, northerly behind
    [InlineData(47.50, 19.03, 90, -20)]  // rode west, easterly behind
    public void Takes_the_ridden_bearing_from_the_two_ends_of_a_stretch(
        double toLatitude, double toLongitude, double windFromBearing, double expected)
    {
        var headwind = HeadwindCalculator.Component(
            from: new GeoPoint(47.50, 19.04),
            to: new GeoPoint(toLatitude, toLongitude),
            windFromBearing,
            windSpeedKmh: 20);

        Assert.Equal(expected, headwind, 0.01);
    }
}
