using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Presents a ride's stored weather, resolving the reported wind against the direction the rider was
/// actually going.
///
/// That resolving happens per sample, not per hour. The wind changes by the hour, but a rider's
/// direction changes with the road, and the two do not line up: an out-and-back turns for home in
/// the middle of an hour, so asking which way that hour went — from where it began to where it
/// ended — asks which way someone went when they went out and came back. The answer is noise. Worse,
/// averaging an hour flattens the part a rider actually remembers, the stretch home with the wind
/// squarely behind.
/// </summary>
public static class RideWeatherReader
{
    public static RideWeather? Read(
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
        var bySample = HeadwindBySample(weather, route, series, rideStart);

        var hours = weather
            .Select(reading => new WeatherHour(
                reading.Hour,
                reading.TemperatureCelsius,
                reading.WindSpeedKmh,
                reading.WindFromBearing,
                HourlyMean(reading, bySample, series, rideStart),
                reading.PrecipitationMm,
                reading.RelativeHumidityPercent,
                reading.CloudCoverPercent,
                reading.WeatherCode))
            .ToList();

        return new RideWeather(hours, bySample);
    }

    /// <summary>
    /// The headwind at each sample: the direction travelled around that point, resolved against the
    /// wind of the hour it fell in. Null where there is no route, no wind reported for that hour, or
    /// the rider was not moving — a stationary point has no direction to resolve against.
    /// </summary>
    private static IReadOnlyList<double?> HeadwindBySample(
        IReadOnlyList<WeatherReading> weather,
        IReadOnlyList<GeoPoint>? route,
        IReadOnlyList<MetricSample>? series,
        DateTimeOffset rideStart)
    {
        if (route is not { Count: > 1 } || series is not { Count: > 1 })
        {
            return [];
        }

        var positions = series
            .Select(sample => RestStopDetector.PositionAtDistanceKm(route, sample.DistanceKm))
            .Select(stop => new GeoPoint(stop.Latitude, stop.Longitude))
            .ToList();

        return series
            .Select((sample, index) =>
            {
                var reading = ReadingAt(weather, rideStart.AddMinutes(sample.ElapsedMinutes));
                if (reading is not { WindSpeedKmh: { } speed, WindFromBearing: { } bearing })
                {
                    return (double?)null;
                }

                // Centred on the sample where possible, so the direction describes the rider passing
                // through the point rather than only what happened after it.
                var from = positions[Math.Max(0, index - 1)];
                var to = positions[Math.Min(positions.Count - 1, index + 1)];

                return from == to ? null : HeadwindCalculator.Component(from, to, bearing, speed);
            })
            .ToList();
    }

    /// <summary>
    /// An hour's headwind: the mean of its own samples, each weighted by the distance it stands for
    /// rather than counted equally, so a slow climb does not outvote a fast descent.
    ///
    /// Samples are grouped by the hour they fall in, which is the same hour whose wind produced
    /// their value — grouping any other way would average figures computed from different winds. An
    /// hour split between out and back nets out near zero, which is the honest answer for it: the
    /// shape of that hour belongs to the graph, not to one number.
    /// </summary>
    private static double? HourlyMean(
        WeatherReading reading,
        IReadOnlyList<double?> bySample,
        IReadOnlyList<MetricSample>? series,
        DateTimeOffset rideStart)
    {
        if (series is not { Count: > 1 } || bySample.Count != series.Count)
        {
            return null;
        }

        var fromMinutes = (reading.Hour - rideStart).TotalMinutes;
        double weighted = 0, distance = 0;

        for (var i = 0; i < series.Count; i++)
        {
            if (series[i].ElapsedMinutes < fromMinutes
                || series[i].ElapsedMinutes >= fromMinutes + 60
                || bySample[i] is not { } headwind)
            {
                continue;
            }

            // Half the stretch either side: what this sample stands for on the road.
            var before = i > 0 ? series[i].DistanceKm - series[i - 1].DistanceKm : 0;
            var after = i < series.Count - 1 ? series[i + 1].DistanceKm - series[i].DistanceKm : 0;
            var stands = (before + after) / 2;

            weighted += headwind * stands;
            distance += stands;
        }

        return distance > 0 ? weighted / distance : null;
    }

    /// <summary>The reported hour a moment falls in, if the ride's weather covers it.</summary>
    private static WeatherReading? ReadingAt(IReadOnlyList<WeatherReading> weather, DateTimeOffset moment)
    {
        foreach (var reading in weather)
        {
            if (moment >= reading.Hour && moment < reading.Hour.AddHours(1))
            {
                return reading;
            }
        }

        return null;
    }
}
