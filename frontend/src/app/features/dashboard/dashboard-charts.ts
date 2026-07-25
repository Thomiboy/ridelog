import type { ChartData, ChartOptions } from 'chart.js';
import type { MonthlyAverageTemperature, MonthlyDistance, MonthlySpeed } from '../../core/api/dashboard.models';

export const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

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

/** Month key "YYYY-MM" for a trend point. */
function monthLabel(point: { year: number; month: number }): string {
  return `${point.year}-${String(point.month).padStart(2, '0')}`;
}

/**
 * Average speed and average temperature per month on one line chart: speed on the left y-axis,
 * temperature on the right. Both series share the same 12-month labels (from the speed trend);
 * empty months stay as gaps on both lines.
 */
export function buildSpeedAndTemperatureTrendChart(
  speed: MonthlySpeed[],
  temperature: MonthlyAverageTemperature[],
): ChartData<'line'> {
  return {
    labels: speed.map(monthLabel),
    datasets: [
      {
        label: 'km/h',
        yAxisID: 'speed',
        data: speed.map((t) => t.averageSpeedKmh ?? null),
        spanGaps: false,
      },
      {
        label: '°C',
        yAxisID: 'temp',
        data: temperature.map((t) => t.averageTemperatureCelsius ?? null),
        spanGaps: false,
      },
    ],
  };
}

/** Dual-axis scales for the speed/temperature trend: speed left, temperature right (no extra gridlines). */
export const SPEED_TEMPERATURE_TREND_OPTIONS: ChartOptions<'line'> = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { intersect: false, mode: 'index' },
  scales: {
    speed: { type: 'linear', position: 'left' },
    temp: { type: 'linear', position: 'right', grid: { drawOnChartArea: false } },
  },
};
