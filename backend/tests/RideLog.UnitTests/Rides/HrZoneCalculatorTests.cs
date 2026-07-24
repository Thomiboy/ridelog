using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

public sealed class HrZoneCalculatorTests
{
    private static MetricSample At(double elapsedMinutes, int? heartRate) =>
        new(DistanceKm: 0, elapsedMinutes, ElevationMeters: null, heartRate);

    [Fact]
    public void Attributes_each_interval_to_the_zone_of_its_starting_sample()
    {
        // maxHR 200 → zone floors at 100/120/140/160/180 bpm (50/60/70/80/90%).
        var series = new List<MetricSample>
        {
            At(0, 130),  // Z2 (120–140) owns 0→10
            At(10, 150), // Z3 (140–160) owns 10→20
            At(20, 170), // Z4 (160–180) owns 20→30
            At(30, 190), // Z5 (≥180): last sample, owns no forward interval
        };

        var zones = HrZoneCalculator.TimeInZone(series, maxHeartRate: 200);

        Assert.Equal(5, zones.Count);
        Assert.Equal(1, zones[0].Zone);
        Assert.Equal(0, Minutes(zones, 1), 0.01); // Z1
        Assert.Equal(10, Minutes(zones, 2), 0.01); // Z2
        Assert.Equal(10, Minutes(zones, 3), 0.01); // Z3
        Assert.Equal(10, Minutes(zones, 4), 0.01); // Z4
        Assert.Equal(0, Minutes(zones, 5), 0.01); // Z5
    }

    [Fact]
    public void Ignores_intervals_below_the_first_zone_or_without_a_reading()
    {
        var series = new List<MetricSample>
        {
            At(0, 80),    // 40% of 200 → below Z1 (100), its 0→10 interval is excluded
            At(10, null), // no reading → its 10→25 interval is excluded
            At(25, 150),  // Z3 owns 25→30
            At(30, 150),
        };

        var zones = HrZoneCalculator.TimeInZone(series, maxHeartRate: 200);

        Assert.Equal(0, Minutes(zones, 1), 0.01);
        Assert.Equal(5, Minutes(zones, 3), 0.01); // only the 25→30 interval counted
    }

    private static double Minutes(IReadOnlyList<HrZoneSlice> zones, int zone) =>
        zones.Single(z => z.Zone == zone).Minutes;
}
