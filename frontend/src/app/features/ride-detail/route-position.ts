const EARTH_RADIUS_KM = 6371;

/**
 * Where along a route a given cumulative distance falls, as [latitude, longitude].
 *
 * This is the frontend counterpart of the backend's rest-stop placement: the ride graph knows how
 * far into a ride a point is, the route knows where that is on the ground, and this joins the two.
 * It lives here rather than in the map component because it is about a ride, not about a map — the
 * map is told where to draw, and stays swappable for it.
 *
 * Distances beyond either end clamp to that end: a route and a metric series are downsampled
 * separately, so their totals do not agree to the metre.
 */
export function positionAtDistanceKm(route: readonly [number, number][], km: number): [number, number] {
  if (route.length === 0) {
    return [0, 0];
  }

  if (km <= 0) {
    return route[0];
  }

  let travelled = 0;
  for (let i = 1; i < route.length; i++) {
    const segment = distanceKm(route[i - 1], route[i]);
    if (travelled + segment >= km) {
      const fraction = segment > 0 ? (km - travelled) / segment : 0;
      return [
        route[i - 1][0] + (route[i][0] - route[i - 1][0]) * fraction,
        route[i - 1][1] + (route[i][1] - route[i - 1][1]) * fraction,
      ];
    }
    travelled += segment;
  }

  return route[route.length - 1];
}

/** Great-circle (haversine) distance between two points, in kilometres. */
function distanceKm([lat1, lon1]: readonly [number, number], [lat2, lon2]: readonly [number, number]): number {
  const toRadians = (degrees: number) => (degrees * Math.PI) / 180;
  const dLat = toRadians(lat2 - lat1);
  const dLon = toRadians(lon2 - lon1);

  const h =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(dLon / 2) ** 2;

  return 2 * EARTH_RADIUS_KM * Math.asin(Math.min(1, Math.sqrt(h)));
}

/** Where on a route a point on the map lands, and how convincingly. */
export interface RouteHit {
  position: [number, number];
  /** How far along the route that point is — the ride's own coordinate, which the graph shares. */
  distanceKm: number;
  /** How far the target was from the route, so the caller can decide whether it was pointing at it. */
  offRouteKm: number;
}

/**
 * The point on a route nearest a position on the map, with its distance along the ride.
 *
 * Snapping to a recorded point rather than to the nearest place on a segment: the route is stored
 * downsampled, so the points are already an approximation of the road and interpolating between
 * them would add precision the data does not have.
 *
 * A straight scan. A route is capped at a thousand points, which is a few tens of microseconds —
 * far too little to be worth an index that would then need keeping in step with the route.
 */
export function nearestOnRoute(route: readonly [number, number][], target: readonly [number, number]): RouteHit {
  let travelled = 0;
  let best: RouteHit = { position: route[0], distanceKm: 0, offRouteKm: distanceKm(route[0], target) };

  for (let i = 1; i < route.length; i++) {
    travelled += distanceKm(route[i - 1], route[i]);
    const off = distanceKm(route[i], target);
    if (off < best.offRouteKm) {
      best = { position: route[i], distanceKm: travelled, offRouteKm: off };
    }
  }

  return best;
}
