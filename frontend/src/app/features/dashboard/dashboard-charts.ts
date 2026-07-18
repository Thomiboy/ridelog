import type { ChartData } from 'chart.js';
import type { MonthlyDistance, MonthlySpeed } from '../../core/api/dashboard.models';

const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** Monthly distance as a two-series bar chart: current year vs previous year, Jan–Dec. */
export function buildMonthlyDistanceChart(monthly: MonthlyDistance[]): ChartData<'bar'> {
  const years = [...new Set(monthly.map((m) => m.year))].sort();

  return {
    labels: [...MONTH_LABELS],
    datasets: years
      .slice()
      .reverse() // current year first in the legend
      .map((year) => ({
        label: String(year),
        data: MONTH_LABELS.map((_, index) => monthly.find((m) => m.year === year && m.month === index + 1)?.distanceKm ?? 0),
      })),
  };
}

/** Average speed per month as a single line; empty months stay as gaps. */
export function buildSpeedTrendChart(trend: MonthlySpeed[]): ChartData<'line'> {
  return {
    labels: trend.map((t) => `${t.year}-${String(t.month).padStart(2, '0')}`),
    datasets: [
      {
        label: 'km/h',
        data: trend.map((t) => t.averageSpeedKmh ?? null),
        spanGaps: false,
      },
    ],
  };
}
