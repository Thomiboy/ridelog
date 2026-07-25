import type { RideSummary } from '../../core/api/ride.models';

/** One cell of the month grid: a day, its rides, and a distance-scaled colour intensity. */
export interface CalendarDay {
  date: Date;
  /** True when the day belongs to the displayed month (false for the grid's leading/trailing days). */
  inMonth: boolean;
  totalKm: number;
  rideCount: number;
  rides: RideSummary[];
  /** 0–1, the day's total distance relative to the busiest loaded day (self-calibrating shade). */
  intensity: number;
}

/** A month laid out as Monday-first weeks of seven days. */
export interface CalendarMonth {
  year: number;
  /** 1-based month number. */
  month: number;
  weeks: CalendarDay[][];
}

/** Local y-m-d key so rides group by the calendar day they're shown on. */
function dayKey(date: Date): string {
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
}

/**
 * Lays out the given month as a grid, filling each day from the rides that fall on it. Colour
 * intensity is scaled to the busiest day across *all* the rides passed in, so the shading calibrates
 * to the loaded data rather than to the visible month.
 */
export function buildCalendarMonth(rides: RideSummary[], year: number, month: number): CalendarMonth {
  const byDay = new Map<string, RideSummary[]>();
  for (const ride of rides) {
    const key = dayKey(new Date(ride.startTime));
    const day = byDay.get(key);
    if (day) {
      day.push(ride);
    } else {
      byDay.set(key, [ride]);
    }
  }

  let maxDailyKm = 0;
  for (const day of byDay.values()) {
    const total = day.reduce((sum, ride) => sum + ride.distanceKm, 0);
    maxDailyKm = Math.max(maxDailyKm, total);
  }

  // Start on the Monday on or before the 1st; end after the week that holds the last day.
  const first = new Date(year, month - 1, 1);
  const lastDay = new Date(year, month, 0);
  const cursor = new Date(first);
  cursor.setDate(1 - ((first.getDay() + 6) % 7));

  const weeks: CalendarDay[][] = [];
  do {
    const week: CalendarDay[] = [];
    for (let i = 0; i < 7; i++) {
      const date = new Date(cursor);
      const dayRides = byDay.get(dayKey(date)) ?? [];
      const totalKm = dayRides.reduce((sum, ride) => sum + ride.distanceKm, 0);
      week.push({
        date,
        inMonth: date.getMonth() === month - 1,
        totalKm,
        rideCount: dayRides.length,
        rides: dayRides,
        intensity: maxDailyKm > 0 ? totalKm / maxDailyKm : 0,
      });
      cursor.setDate(cursor.getDate() + 1);
    }
    weeks.push(week);
  } while (cursor <= lastDay);

  return { year, month, weeks };
}
