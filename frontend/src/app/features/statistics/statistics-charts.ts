import type { ChartData } from 'chart.js';
import type { MonthlyAggregate } from '../../core/api/statistics.models';

export const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** A numeric per-month metric of a monthly aggregate. */
export type MonthlyMetric = 'distanceKm' | 'elevationGainMeters' | 'rideCount' | 'calories';

/** The distinct years that have cycling data, ascending — drives the year selector. */
export function statisticsYears(monthly: MonthlyAggregate[]): number[] {
  return [...new Set(monthly.map((m) => m.year))].sort((a, b) => a - b);
}

/** One metric for the selected year as a single-series Jan–Dec bar chart, zero-filled. */
export function buildMonthlyMetricChart(
  monthly: MonthlyAggregate[],
  year: number,
  metric: MonthlyMetric,
): ChartData<'bar'> {
  return {
    labels: [...MONTH_LABELS],
    datasets: [
      {
        label: String(year),
        data: MONTH_LABELS.map((_, index) => monthly.find((m) => m.year === year && m.month === index + 1)?.[metric] ?? 0),
      },
    ],
  };
}

/** Total distance per year across every year with data, as a single-series bar chart. */
export function buildYearTotalsChart(monthly: MonthlyAggregate[]): ChartData<'bar'> {
  const years = statisticsYears(monthly);
  return {
    labels: years.map(String),
    datasets: [
      {
        label: 'km',
        data: years.map(
          (year) => Math.round(monthly.filter((m) => m.year === year).reduce((sum, m) => sum + m.distanceKm, 0) * 10) / 10,
        ),
      },
    ],
  };
}
