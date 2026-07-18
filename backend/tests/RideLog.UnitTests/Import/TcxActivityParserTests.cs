using System.Text;
using RideLog.Application.Import;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

public class TcxActivityParserTests
{
    private const string Tcx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities>
            <Activity Sport="Biking">
              <Id>2026-06-02T07:00:00Z</Id>
              <Lap StartTime="2026-06-02T07:00:00Z">
                <TotalTimeSeconds>3000</TotalTimeSeconds>
                <DistanceMeters>30000</DistanceMeters>
                <MaximumSpeed>16.5</MaximumSpeed>
                <Calories>620</Calories>
                <Extensions>
                  <LX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2">
                    <AvgSpeed>10.0</AvgSpeed>
                  </LX>
                </Extensions>
                <Track>
                  <Trackpoint>
                    <Time>2026-06-02T07:00:00Z</Time>
                    <Position><LatitudeDegrees>0.0</LatitudeDegrees><LongitudeDegrees>0.0</LongitudeDegrees></Position>
                    <AltitudeMeters>100</AltitudeMeters>
                    <HeartRateBpm><Value>120</Value></HeartRateBpm>
                    <Cadence>80</Cadence>
                  </Trackpoint>
                  <Trackpoint>
                    <Time>2026-06-02T07:30:00Z</Time>
                    <Position><LatitudeDegrees>0.0</LatitudeDegrees><LongitudeDegrees>0.01</LongitudeDegrees></Position>
                    <AltitudeMeters>130</AltitudeMeters>
                    <HeartRateBpm><Value>140</Value></HeartRateBpm>
                    <Cadence>90</Cadence>
                  </Trackpoint>
                  <Trackpoint>
                    <Time>2026-06-02T08:00:00Z</Time>
                    <Position><LatitudeDegrees>0.0</LatitudeDegrees><LongitudeDegrees>0.02</LongitudeDegrees></Position>
                    <AltitudeMeters>120</AltitudeMeters>
                    <HeartRateBpm><Value>160</Value></HeartRateBpm>
                    <Cadence>88</Cadence>
                  </Trackpoint>
                </Track>
              </Lap>
            </Activity>
          </Activities>
        </TrainingCenterDatabase>
        """;

    private static ParsedActivity Parse()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Tcx));
        return new TcxActivityParser().Parse(stream, "ride.tcx");
    }

    [Fact]
    public void Recognises_tcx_files() =>
        Assert.True(new TcxActivityParser().CanParse("Evening Ride.TCX"));

    [Fact]
    public void Reads_sport_time_bounds_and_route()
    {
        var activity = Parse();

        Assert.Equal("Biking", activity.Sport);
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 7, 0, 0, TimeSpan.Zero), activity.StartTime);
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero), activity.EndTime);
        Assert.Equal(3, activity.RoutePoints.Count);
    }

    [Fact]
    public void Takes_distance_from_the_lap_total()
    {
        Assert.Equal(30000, Parse().DistanceMeters, 0.001);
    }

    [Fact]
    public void Averages_and_peaks_heart_rate()
    {
        var activity = Parse();

        Assert.Equal(140, activity.AverageHeartRate); // (120 + 140 + 160) / 3
        Assert.Equal(160, activity.MaximumHeartRate);
    }

    [Fact]
    public void Averages_cadence()
    {
        Assert.Equal(86, Parse().AverageCadence); // (80 + 90 + 88) / 3 → 86
    }

    [Fact]
    public void Takes_maximum_speed_from_the_lap_in_kmh()
    {
        // Lap MaximumSpeed is metres per second; 16.5 m/s × 3.6 = 59.4 km/h.
        Assert.Equal(59.4, Parse().MaximumSpeedKmh!.Value, 0.01);
    }

    [Fact]
    public void Sums_calories_across_laps()
    {
        Assert.Equal(620, Parse().Calories);
    }

    [Fact]
    public void Prefers_the_source_average_speed_over_elapsed_time()
    {
        // Lap LX AvgSpeed is 10.0 m/s → 36.0 km/h. The elapsed-time derivation would give
        // 30 km / 1 h = 30.0 km/h, so the source value must win.
        Assert.Equal(36.0, Parse().AverageSpeedKmh!.Value, 0.01);
    }

    [Fact]
    public void Falls_back_to_moving_time_when_no_source_average_speed()
    {
        // No LX AvgSpeed: derive from distance and moving time (TotalTimeSeconds), not elapsed
        // wall time. 20 km over 2400 s (40 min) = 30.0 km/h; elapsed wall time is 1 h → 20 km/h.
        var tcx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Biking">
                  <Id>2026-06-02T07:00:00Z</Id>
                  <Lap StartTime="2026-06-02T07:00:00Z">
                    <TotalTimeSeconds>2400</TotalTimeSeconds>
                    <DistanceMeters>20000</DistanceMeters>
                    <Track>
                      <Trackpoint>
                        <Time>2026-06-02T07:00:00Z</Time>
                        <Position><LatitudeDegrees>0.0</LatitudeDegrees><LongitudeDegrees>0.0</LongitudeDegrees></Position>
                      </Trackpoint>
                      <Trackpoint>
                        <Time>2026-06-02T08:00:00Z</Time>
                        <Position><LatitudeDegrees>0.0</LatitudeDegrees><LongitudeDegrees>0.02</LongitudeDegrees></Position>
                      </Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(tcx));

        var activity = new TcxActivityParser().Parse(stream, "ride.tcx");

        Assert.Equal(30.0, activity.AverageSpeedKmh!.Value, 0.01);
    }

    [Fact]
    public void Sums_only_positive_elevation_changes()
    {
        // 100 → 130 → 120: +30 climb counts, −10 descent ignored.
        Assert.Equal(30d, Parse().ElevationGainMeters!.Value, 0.1);
    }
}
