using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Presents a ride's stored weather, resolving each hour's reported wind against the direction the
/// rider was actually going during that hour. A loop returns whatever it borrowed, so a single
/// figure for the whole ride would say nothing — the hour is the smallest slice the data supports.
/// </summary>
public static class RideWeatherReader
{
    public static IReadOnlyList<WeatherHour>? Read(
        IReadOnlyList<WeatherReading>? weather,
        string? routePolyline,
        IReadOnlyList<MetricSample>? series,
        DateTimeOffset rideStart)
    {
        if (weather is not { Count: > 0 })
        {
            return null;
        }

        var route = routePolyline is null ? null : PolylineDecoder.Decode(routePolyline);

        return weather
            .Select(reading => new WeatherHour(
                reading.Hour,
                reading.TemperatureCelsius,
                reading.WindSpeedKmh,
                reading.WindFromBearing,
                Headwind(reading, route, series, rideStart),
                reading.PrecipitationMm,
                reading.RelativeHumidityPercent,
                reading.CloudCoverPercent,
                reading.WeatherCode))
            .ToList();
    }

    private static double? Headwind(
        WeatherReading reading,
        IReadOnlyList<GeoPoint>? route,
        IReadOnlyList<MetricSample>? series,
        DateTimeOffset rideStart)
    {
        if (reading is not { WindSpeedKmh: { } speed, WindFromBearing: { } bearing }
            || route is not { Count: > 1 }
            || series is not { Count: > 1 })
        {
            return null;
        }

        // Where the rider was at the start and end of this hour, found by turning the hour's edges
        // into elapsed minutes, those into a distance along the route, and that into a position.
        var fromMinutes = (reading.Hour - rideStart).TotalMinutes;
        var toMinutes = fromMinutes + 60;
        var from = PositionAtMinutes(route, series, fromMinutes);
        var to = PositionAtMinutes(route, series, toMinutes);

        // A stretch that goes nowhere — the hour fell outside the ride, or the rider was stopped for
        // all of it — gives no direction to resolve against.
        return from == to ? null : HeadwindCalculator.Component(from, to, bearing, speed);
    }

    private static GeoPoint PositionAtMinutes(
        IReadOnlyList<GeoPoint> route, IReadOnlyList<MetricSample> series, double elapsedMinutes)
    {
        var position = RestStopDetector.PositionAtDistanceKm(route, DistanceAtMinutes(series, elapsedMinutes));
        return new GeoPoint(position.Latitude, position.Longitude);
    }

    /// <summary>How far along the ride was at a given elapsed minute, straight-line between samples.</summary>
    private static double DistanceAtMinutes(IReadOnlyList<MetricSample> series, double elapsedMinutes)
    {
        if (elapsedMinutes <= series[0].ElapsedMinutes)
        {
            return series[0].DistanceKm;
        }

        for (var i = 1; i < series.Count; i++)
        {
            if (series[i].ElapsedMinutes < elapsedMinutes)
            {
                continue;
            }

            var span = series[i].ElapsedMinutes - series[i - 1].ElapsedMinutes;
            var fraction = span > 0 ? (elapsedMinutes - series[i - 1].ElapsedMinutes) / span : 0;
            return series[i - 1].DistanceKm + ((series[i].DistanceKm - series[i - 1].DistanceKm) * fraction);
        }

        return series[^1].DistanceKm;
    }
}
