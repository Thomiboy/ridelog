import { compareRides, type MetricDelta } from './ride-comparison';
import type { RideDetail } from '../../core/api/ride.models';

const base: RideDetail = {
  id: 'a',
  startTime: '2026-06-01T08:00:00Z',
  endTime: '2026-06-01T10:00:00Z',
  distanceKm: 60,
  durationMinutes: 120,
  sport: 'ROAD_BIKING',
  sportCategory: 'Cycling',
  sources: [],
  averageSpeedKmh: 30,
  maximumSpeedKmh: 55,
  averageHeartRate: 140,
  maximumHeartRate: 175,
  elevationGainMeters: 500,
  calories: 800,
};

const find = (deltas: MetricDelta[], key: string) => deltas.find((d) => d.key === key)!;

describe('compareRides', () => {
  it('marks a bigger distance as better (more is better) with the signed delta', () => {
    const a = { ...base, distanceKm: 60 };
    const b = { ...base, distanceKm: 50 };

    const distance = find(compareRides(a, b), 'distance');
    expect(distance.a).toBe(60);
    expect(distance.b).toBe(50);
    expect(distance.delta).toBe(10);
    expect(distance.direction).toBe('better');
  });

  it('marks a smaller directional metric as worse', () => {
    const a = { ...base, elevationGainMeters: 300 };
    const b = { ...base, elevationGainMeters: 500 };

    expect(find(compareRides(a, b), 'elevation').direction).toBe('worse');
  });

  it('leaves neutral metrics (heart rate, duration) without a better/worse judgement', () => {
    const a = { ...base, averageHeartRate: 150, durationMinutes: 130 };
    const b = { ...base, averageHeartRate: 140, durationMinutes: 120 };

    const deltas = compareRides(a, b);
    expect(find(deltas, 'avgHeartRate').direction).toBe('neutral');
    expect(find(deltas, 'avgHeartRate').delta).toBe(10);
    expect(find(deltas, 'duration').direction).toBe('neutral');
  });

  it('yields a null delta and neutral direction when either ride lacks the metric', () => {
    const a = { ...base, maximumSpeedKmh: undefined };
    const b = { ...base, maximumSpeedKmh: 55 };

    const maxSpeed = find(compareRides(a, b), 'maxSpeed');
    expect(maxSpeed.a).toBeNull();
    expect(maxSpeed.delta).toBeNull();
    expect(maxSpeed.direction).toBe('neutral');
  });
});
