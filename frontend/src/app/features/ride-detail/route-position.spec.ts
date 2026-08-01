import { describe, expect, it } from 'vitest';
import { nearestOnRoute, positionAtDistanceKm } from './route-position';

// One degree of longitude at the equator is 2πR/360 ≈ 111.195 km, so this segment is 1.11195 km
// long. Every expectation below is worked out from that, not from what the function returns.
const EQUATOR_SEGMENT: [number, number][] = [
  [0, 0],
  [0, 0.01],
];

describe('positionAtDistanceKm', () => {
  it('interpolates along the segment the distance falls in', () => {
    const [latitude, longitude] = positionAtDistanceKm(EQUATOR_SEGMENT, 1.11195 / 2);

    expect(latitude).toBeCloseTo(0, 6);
    expect(longitude).toBeCloseTo(0.005, 5);
  });

  // A ride's route and its metric series are downsampled separately, so a distance can land just
  // outside the route's own total. Clamping keeps the marker on the road rather than nowhere.
  it('clamps to the ends rather than running off them', () => {
    expect(positionAtDistanceKm(EQUATOR_SEGMENT, -5)).toEqual([0, 0]);
    expect(positionAtDistanceKm(EQUATOR_SEGMENT, 999)).toEqual([0, 0.01]);
  });
});

// Three points a degree-hundredth apart along the equator: 0, 1.11195 and 2.2239 km in.
const EQUATOR_ROUTE: [number, number][] = [
  [0, 0],
  [0, 0.01],
  [0, 0.02],
];

describe('nearestOnRoute', () => {
  // Pointing at the map has to come back as a distance along the ride, because that is what the
  // graph is indexed by. The middle point is 1.11195 km in, and a target a whisker past it is
  // nearer to it than to either end.
  it('answers with the nearest point and how far along the route it is', () => {
    const hit = nearestOnRoute(EQUATOR_ROUTE, [0, 0.0101]);

    expect(hit.position).toEqual([0, 0.01]);
    expect(hit.distanceKm).toBeCloseTo(1.11195, 3);
  });

  // How far off the pointer was is what decides whether it counts as pointing at the route at all,
  // so the caller needs it back. A degree-hundredth of latitude is 1.11195 km north of the line.
  it('reports how far the target was from the route', () => {
    const hit = nearestOnRoute(EQUATOR_ROUTE, [0.01, 0.01]);

    expect(hit.offRouteKm).toBeCloseTo(1.11195, 3);
  });
});
