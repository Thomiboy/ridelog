using RideLog.Application.Routes;

namespace RideLog.UnitTests.Routes;

public class PolylineDecoderTests
{
    [Fact]
    public void Decodes_the_reference_route_from_the_google_algorithm_spec()
    {
        // The inverse of the encoder's worked example.
        var points = PolylineDecoder.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@");

        Assert.Equal(3, points.Count);
        Assert.Equal(38.5, points[0].Latitude, 5);
        Assert.Equal(-120.2, points[0].Longitude, 5);
        Assert.Equal(40.7, points[1].Latitude, 5);
        Assert.Equal(-120.95, points[1].Longitude, 5);
        Assert.Equal(43.252, points[2].Latitude, 5);
        Assert.Equal(-126.453, points[2].Longitude, 5);
    }

    [Fact]
    public void Empty_string_decodes_to_an_empty_route()
    {
        Assert.Empty(PolylineDecoder.Decode(string.Empty));
    }
}
