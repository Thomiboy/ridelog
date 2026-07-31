using RideLog.Application.Routes;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Resolves reported wind onto the direction actually being ridden. Weather services report the
/// bearing wind blows *from*, so a wind and a rider sharing a bearing are meeting head-on.
/// </summary>
public static class HeadwindCalculator
{
    /// <summary>
    /// The share of <paramref name="windSpeedKmh"/> acting along the ridden line: positive is
    /// headwind, negative is tailwind, and a crosswind resolves to zero because none of it opposes
    /// or assists. Bearings are degrees clockwise from north.
    /// </summary>
    public static double Component(double ridingBearing, double windFromBearing, double windSpeedKmh)
        => windSpeedKmh * Math.Cos((windFromBearing - ridingBearing) * Math.PI / 180);

    /// <summary>
    /// The same, for a stretch of route: the direction ridden is the initial great-circle bearing
    /// from one end to the other, which treats the stretch as the straight line it stands in for.
    /// </summary>
    public static double Component(GeoPoint from, GeoPoint to, double windFromBearing, double windSpeedKmh)
        => Component(BearingDegrees(from, to), windFromBearing, windSpeedKmh);

    private static double BearingDegrees(GeoPoint from, GeoPoint to)
    {
        var lat1 = ToRadians(from.Latitude);
        var lat2 = ToRadians(to.Latitude);
        var dLon = ToRadians(to.Longitude - from.Longitude);

        var y = Math.Sin(dLon) * Math.Cos(lat2);
        var x = (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon));

        // atan2 returns ±180°; bearings are conventionally 0–360 clockwise from north.
        return (ToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
