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
                <TotalTimeSeconds>3600</TotalTimeSeconds>
                <DistanceMeters>30000</DistanceMeters>
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
    public void Sums_only_positive_elevation_changes()
    {
        // 100 → 130 → 120: +30 climb counts, −10 descent ignored.
        Assert.Equal(30d, Parse().ElevationGainMeters!.Value, 0.1);
    }
}
