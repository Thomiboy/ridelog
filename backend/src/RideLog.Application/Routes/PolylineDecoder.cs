namespace RideLog.Application.Routes;

/// <summary>
/// Decodes a Google Encoded Polyline (precision 5) back into points — the inverse of
/// <see cref="PolylineEncoder"/>. Used to position map annotations by cumulative distance.
/// </summary>
public static class PolylineDecoder
{
    public static IReadOnlyList<GeoPoint> Decode(string encoded)
    {
        var points = new List<GeoPoint>();
        var index = 0;
        var lat = 0;
        var lng = 0;

        while (index < encoded.Length)
        {
            lat += DecodeValue(encoded, ref index);
            lng += DecodeValue(encoded, ref index);
            points.Add(new GeoPoint(lat / 1e5, lng / 1e5));
        }

        return points;
    }

    private static int DecodeValue(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int chunk;
        do
        {
            chunk = encoded[index++] - 63;
            result |= (chunk & 0x1f) << shift;
            shift += 5;
        }
        while (chunk >= 0x20);

        // Inverse of the encoder: undo the left-shift and the negative-number inversion.
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
