using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Import;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Finds where a ride paused for more than about a minute, from its downsampled metric series, and
/// places each pause on the decoded route by cumulative distance. Both steps are pure so they can be
/// tested without EF or a real ride.
/// </summary>
public static class RestStopDetector
{
    /// <summary>How long the rider has to be stalled (minutes) before it counts as a rest.</summary>
    private const double PauseMinutes = 1.0;

    /// <summary>
    /// Below this average speed (km/h) over a gap the rider counts as stalled rather than riding —
    /// slower than walking, so it survives the GPS jitter of a stationary bike.
    /// </summary>
    private const double StalledSpeedKmh = 3.0;

    /// <summary>
    /// The cumulative distances (km) at which the rider rested. A rest is a stretch of the ride
    /// spent going nowhere for longer than <see cref="PauseMinutes"/>, recorded at the distance
    /// where it began; consecutive stalled samples collapse into one rest.
    /// </summary>
    /// <remarks>
    /// The stall is measured as a *rate* and accumulated across samples on purpose. The series this
    /// runs on is downsampled to <see cref="Import.MetricSeriesBuilder.MaxSamples"/> points, so
    /// neighbouring samples sit however far apart the ride's length makes them — a couple of hours
    /// lands them ~15 s apart. Asking any single gap to exceed a whole minute would mean no normal
    /// ride could ever report a rest.
    /// </remarks>
    public static IReadOnlyList<double> DetectRestDistancesKm(IReadOnlyList<MetricSample> series)
    {
        var rests = new List<double>();
        var stalledMinutes = 0.0;
        var stallStartKm = 0.0;
        var hasRidden = false;

        for (var i = 1; i < series.Count; i++)
        {
            var elapsedGap = series[i].ElapsedMinutes - series[i - 1].ElapsedMinutes;
            var distanceGap = series[i].DistanceKm - series[i - 1].DistanceKm;
            var stalled = elapsedGap > 0 && distanceGap / (elapsedGap / 60.0) < StalledSpeedKmh;

            if (stalled)
            {
                if (stalledMinutes == 0)
                {
                    stallStartKm = series[i - 1].DistanceKm;
                }
                stalledMinutes += elapsedGap;
                continue;
            }

            // A stall only becomes a rest once the rider sets off again. That's what makes it a pause
            // *within* the ride: standing around before the first pedal stroke, or leaving the
            // recording running afterwards, never gets closed off and so never marks the map.
            if (hasRidden && stalledMinutes > PauseMinutes)
            {
                rests.Add(stallStartKm);
            }

            stalledMinutes = 0;
            hasRidden = true;
        }

        return rests;
    }

    /// <summary>The lat/lng at a cumulative distance (km) along the route, clamped to its ends.</summary>
    public static RestStop PositionAtDistanceKm(IReadOnlyList<GeoPoint> route, double km)
    {
        if (km <= 0)
        {
            return new RestStop(route[0].Latitude, route[0].Longitude);
        }

        var cumulativeKm = 0.0;
        for (var i = 1; i < route.Count; i++)
        {
            var segmentKm = GeoMath.DistanceMeters(route[i - 1], route[i]) / 1000.0;
            if (cumulativeKm + segmentKm >= km)
            {
                var fraction = segmentKm > 0 ? (km - cumulativeKm) / segmentKm : 0;
                var lat = route[i - 1].Latitude + (route[i].Latitude - route[i - 1].Latitude) * fraction;
                var lng = route[i - 1].Longitude + (route[i].Longitude - route[i - 1].Longitude) * fraction;
                return new RestStop(lat, lng);
            }

            cumulativeKm += segmentKm;
        }

        var last = route[^1];
        return new RestStop(last.Latitude, last.Longitude);
    }

    /// <summary>Rest stops for a ride: detected from the series, positioned on the route. Empty when either is missing.</summary>
    public static IReadOnlyList<RestStop> RestStops(IReadOnlyList<MetricSample> series, IReadOnlyList<GeoPoint> route)
    {
        if (series.Count == 0 || route.Count == 0)
        {
            return [];
        }

        return DetectRestDistancesKm(series)
            .Select(km => PositionAtDistanceKm(route, km))
            .ToList();
    }
}
