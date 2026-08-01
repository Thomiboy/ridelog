import { describe, expect, it } from 'vitest';
import { positionAtDistanceKm } from './route-position';

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
