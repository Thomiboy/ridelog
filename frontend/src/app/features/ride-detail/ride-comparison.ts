import type { RideDetail } from '../../core/api/ride.models';

/** Whether the current ride's value is an improvement over the compared one; neutral = no judgement. */
export type Direction = 'better' | 'worse' | 'neutral';

/** One metric compared between the current ride (a) and the selected earlier ride (b). */
export interface MetricDelta {
  key: string;
  a: number | null;
  b: number | null;
  /** a − b, or null when either side is missing. */
  delta: number | null;
  direction: Direction;
}

interface MetricDef {
  key: string;
  value: (ride: RideDetail) => number | null | undefined;
  /** True when a higher value is better (distance, speed, climbing); false = no judgement. */
  higherIsBetter: boolean;
}

// Order drives the comparison table. Directional metrics get a better/worse arrow; the rest are neutral.
const METRICS: MetricDef[] = [
  { key: 'distance', value: (r) => r.distanceKm, higherIsBetter: true },
  { key: 'duration', value: (r) => r.durationMinutes, higherIsBetter: false },
  { key: 'avgSpeed', value: (r) => r.averageSpeedKmh, higherIsBetter: true },
  { key: 'maxSpeed', value: (r) => r.maximumSpeedKmh, higherIsBetter: true },
  { key: 'avgHeartRate', value: (r) => r.averageHeartRate, higherIsBetter: false },
  { key: 'maxHeartRate', value: (r) => r.maximumHeartRate, higherIsBetter: false },
  { key: 'elevation', value: (r) => r.elevationGainMeters, higherIsBetter: true },
  { key: 'calories', value: (r) => r.calories, higherIsBetter: false },
];

/** Compares the current ride against an earlier one, metric by metric, in display order. */
export function compareRides(a: RideDetail, b: RideDetail): MetricDelta[] {
  return METRICS.map((metric) => {
    const valueA = metric.value(a) ?? null;
    const valueB = metric.value(b) ?? null;
    if (valueA === null || valueB === null) {
      return { key: metric.key, a: valueA, b: valueB, delta: null, direction: 'neutral' as const };
    }

    const delta = Math.round((valueA - valueB) * 1000) / 1000;
    const direction: Direction =
      !metric.higherIsBetter || delta === 0 ? 'neutral' : delta > 0 ? 'better' : 'worse';
    return { key: metric.key, a: valueA, b: valueB, delta, direction };
  });
}
