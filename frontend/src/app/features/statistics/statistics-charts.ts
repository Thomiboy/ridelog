import type { ChartData } from 'chart.js';
import type { MonthlyAggregate, MonthlyTemperature, TemperatureBandSlice } from '../../core/api/statistics.models';

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

/** Human label for a temperature band: "<0°", "5–10°", "25°+". */
export function bandLabel(band: TemperatureBandSlice): string {
  if (band.fromCelsius == null) {
    return `<${band.toCelsius}°`;
  }
  if (band.toCelsius == null) {
    return `${band.fromCelsius}°+`;
  }
  return `${band.fromCelsius}–${band.toCelsius}°`;
}

/** Band colour by its temperature range: deep blue (cold) through green to red (hot). */
const BAND_COLORS_BY_FLOOR: Record<number, string> = {
  0: '#1e88e5', // 0–5 blue
  5: '#4fc3f7', // 5–10 light blue
  10: '#66bb6a', // 10–15 green
  15: '#fdd835', // 15–20 yellow
  20: '#fb8c00', // 20–25 orange
  25: '#e53935', // 25+ red
};

function bandColor(band: TemperatureBandSlice): string {
  return band.fromCelsius == null ? '#0d47a1' : (BAND_COLORS_BY_FLOOR[band.fromCelsius] ?? '#9e9e9e');
}

/** Distance per 5°C temperature band as a bar chart, colour-coded cold to hot. */
export function buildTemperatureDistributionChart(bands: TemperatureBandSlice[]): ChartData<'bar'> {
  return {
    labels: bands.map(bandLabel),
    datasets: [{ label: 'km', data: bands.map((b) => b.km), backgroundColor: bands.map(bandColor) }],
  };
}

/** Average ridden temperature per month as a line chart. */
export function buildTemperatureTrendChart(monthly: MonthlyTemperature[]): ChartData<'line'> {
  return {
    labels: monthly.map((m) => `${m.year}-${String(m.month).padStart(2, '0')}`),
    datasets: [{ label: '°C', data: monthly.map((m) => m.averageTemperatureCelsius) }],
  };
}
