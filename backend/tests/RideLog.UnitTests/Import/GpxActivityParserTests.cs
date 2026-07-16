using System.Text;
using RideLog.Application.Import;
using RideLog.Infrastructure.Import;

namespace RideLog.UnitTests.Import;

public class GpxActivityParserTests
{
    // Two 1°-of-latitude hops along the same meridian; elevation rises then dips.
    private const string Gpx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
          <trk>
            <type>cycling</type>
            <trkseg>
              <trkpt lat="0.0" lon="0.0"><ele>100</ele><time>2026-06-01T08:00:00Z</time></trkpt>
              <trkpt lat="1.0" lon="0.0"><ele>110</ele><time>2026-06-01T08:30:00Z</time></trkpt>
              <trkpt lat="2.0" lon="0.0"><ele>105</ele><time>2026-06-01T09:00:00Z</time></trkpt>
            </trkseg>
          </trk>
        </gpx>
        """;

    private static ParsedActivity Parse()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Gpx));
        return new GpxActivityParser().Parse(stream, "ride.gpx");
    }

    [Fact]
    public void Recognises_gpx_files() =>
        Assert.True(new GpxActivityParser().CanParse("Morning Ride.GPX"));

    [Fact]
    public void Reads_the_track_points_and_time_bounds()
    {
        var activity = Parse();

        Assert.Equal(3, activity.RoutePoints.Count);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero), activity.StartTime);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), activity.EndTime);
        Assert.Equal(TimeSpan.FromHours(1), activity.Duration);
    }

    [Fact]
    public void Computes_distance_from_the_track()
    {
        // Great-circle arc length of 1° latitude ≈ 111.19 km; two hops ≈ 222.39 km.
        Assert.Equal(222_390d, Parse().DistanceMeters, 300d);
    }

    [Fact]
    public void Sums_only_positive_elevation_changes()
    {
        // 100 → 110 → 105: gain is the +10 climb, the −5 descent is ignored.
        Assert.Equal(10d, Parse().ElevationGainMeters!.Value, 0.1);
    }

    [Fact]
    public void Carries_the_sport_from_the_track_type()
    {
        Assert.Equal("cycling", Parse().Sport);
    }
}
