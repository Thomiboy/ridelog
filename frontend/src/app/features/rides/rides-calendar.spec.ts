import { buildCalendarMonth, type CalendarDay } from './rides-calendar';
import type { RideSummary } from '../../core/api/ride.models';

const ride = (id: string, startTime: string, distanceKm: number): RideSummary => ({
  id,
  startTime,
  distanceKm,
  durationMinutes: 60,
  sport: 'ROAD_BIKING',
  sources: [],
});

/** Finds the grid cell for a given calendar date (local y/m/d). */
function dayOf(month: ReturnType<typeof buildCalendarMonth>, year: number, m: number, d: number): CalendarDay {
  const cell = month.weeks.flat().find((c) => c.date.getFullYear() === year && c.date.getMonth() === m - 1 && c.date.getDate() === d);
  if (!cell) {
    throw new Error(`No cell for ${year}-${m}-${d}`);
  }
  return cell;
}

describe('buildCalendarMonth', () => {
  it("places a day's ride on its date with distance and count", () => {
    const month = buildCalendarMonth([ride('r1', '2026-07-05T08:00:00Z', 42)], 2026, 7);

    const cell = dayOf(month, 2026, 7, 5);
    expect(cell.inMonth).toBe(true);
    expect(cell.totalKm).toBe(42);
    expect(cell.rideCount).toBe(1);
    expect(cell.rides.map((r) => r.id)).toEqual(['r1']);
  });

  it('sums a day with several rides and counts them', () => {
    const month = buildCalendarMonth(
      [ride('r1', '2026-07-05T08:00:00Z', 30), ride('r2', '2026-07-05T16:00:00Z', 20)],
      2026,
      7,
    );

    const cell = dayOf(month, 2026, 7, 5);
    expect(cell.totalKm).toBe(50);
    expect(cell.rideCount).toBe(2);
    expect(cell.rides.map((r) => r.id)).toEqual(['r1', 'r2']);
  });

  it('scales intensity to the busiest loaded day, across every month', () => {
    // The busiest day (100 km) is in a different month than the one we render.
    const month = buildCalendarMonth(
      [ride('busy', '2026-06-10T08:00:00Z', 100), ride('half', '2026-07-05T08:00:00Z', 50)],
      2026,
      7,
    );

    expect(dayOf(month, 2026, 7, 5).intensity).toBeCloseTo(0.5, 5);
  });

  it('leaves ride-free days empty with zero intensity', () => {
    const month = buildCalendarMonth([ride('r1', '2026-07-05T08:00:00Z', 42)], 2026, 7);

    const empty = dayOf(month, 2026, 7, 6);
    expect(empty.totalKm).toBe(0);
    expect(empty.rideCount).toBe(0);
    expect(empty.intensity).toBe(0);
  });

  it('lays out Monday-first weeks with adjacent-month days marked out-of-month', () => {
    // July 2026: the 1st is a Wednesday, so the first week starts on Mon Jun 29.
    const month = buildCalendarMonth([], 2026, 7);

    expect(month.weeks[0]).toHaveLength(7);
    expect(month.weeks[0][0].date.getDay()).toBe(1); // Monday
    expect(month.weeks[0][0].date.getDate()).toBe(29); // Jun 29
    expect(month.weeks[0][0].inMonth).toBe(false); // previous month
    expect(dayOf(month, 2026, 7, 1).inMonth).toBe(true);
  });
});
