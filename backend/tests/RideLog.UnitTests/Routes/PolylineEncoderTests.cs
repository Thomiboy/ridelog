using RideLog.Application.Routes;

namespace RideLog.UnitTests.Routes;

public class PolylineEncoderTests
{
    [Fact]
    public void Encodes_the_reference_route_from_the_google_algorithm_spec()
    {
        // Worked example from Google's Encoded Polyline Algorithm documentation.
        GeoPoint[] points =
        [
            new(38.5, -120.2),
            new(40.7, -120.95),
            new(43.252, -126.453),
        ];

        var encoded = PolylineEncoder.Encode(points);

        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", encoded);
    }

    [Fact]
    public void Empty_route_encodes_to_an_empty_string()
    {
        Assert.Equal(string.Empty, PolylineEncoder.Encode([]));
    }
}
