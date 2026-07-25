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
    /// <summary>A pause counts once elapsed time jumps by more than this (minutes) between samples.</summary>
    private const double PauseMinutes = 1.0;

    /// <summary>…while cumulative distance advances by less than this (km) — i.e. the rider barely moved.</summary>
    private const double MovementKm = 0.05;

    /// <summary>
    /// The cumulative distances (km) at which the rider rested. Consecutive paused samples collapse
    /// into one rest, recorded at the distance where the pause began.
    /// </summary>
    public static IReadOnlyList<double> DetectRestDistancesKm(IReadOnlyList<MetricSample> series)
    {
        var rests = new List<double>();
        var inRest = false;
        for (var i = 1; i < series.Count; i++)
        {
            var elapsedGap = series[i].ElapsedMinutes - series[i - 1].ElapsedMinutes;
            var distanceGap = series[i].DistanceKm - series[i - 1].DistanceKm;
            var isRestGap = elapsedGap > PauseMinutes && distanceGap < MovementKm;

            if (isRestGap && !inRest)
            {
                rests.Add(series[i - 1].DistanceKm);
            }
            inRest = isRestGap;
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
