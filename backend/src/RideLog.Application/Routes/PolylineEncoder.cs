using System.Text;

namespace RideLog.Application.Routes;

/// <summary>
/// Encodes a route to a Google Encoded Polyline string (precision 5), the compact form
/// stored on <c>Ride.RoutePolyline</c> and understood by Leaflet's polyline decoders.
/// </summary>
public static class PolylineEncoder
{
    public static string Encode(IEnumerable<GeoPoint> points)
    {
        var builder = new StringBuilder();
        var previousLat = 0;
        var previousLng = 0;

        foreach (var point in points)
        {
            var lat = (int)Math.Round(point.Latitude * 1e5);
            var lng = (int)Math.Round(point.Longitude * 1e5);

            EncodeValue(lat - previousLat, builder);
            EncodeValue(lng - previousLng, builder);

            previousLat = lat;
            previousLng = lng;
        }

        return builder.ToString();
    }

    private static void EncodeValue(int value, StringBuilder builder)
    {
        // Two's-complement, left-shift by one, invert if negative — the standard algorithm.
        var shifted = value << 1;
        if (value < 0)
        {
            shifted = ~shifted;
        }

        while (shifted >= 0x20)
        {
            builder.Append((char)((0x20 | (shifted & 0x1f)) + 63));
            shifted >>= 5;
        }

        builder.Append((char)(shifted + 63));
    }
}
