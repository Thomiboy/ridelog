import type { ChartData } from 'chart.js';
import type {
  MonthlyAggregate,
  MonthlyTemperature,
  TemperatureBandSlice,
  YearlyTemperatureBand,
} from '../../core/api/statistics.models';
import { MONTH_LABELS } from '../../core/i18n/month-labels';

/** A numeric per-month metric of a monthly aggregate. */
export type MonthlyMetric = 'distanceKm' | 'elevationGainMeters' | 'rideCount' | 'calories';

/** The distinct years that have cycling data, ascending — drives the year selector. */
export function statisticsYears(monthly: MonthlyAggregate[]): number[] {
  return [...new Set(monthly.map((m) => m.year))].sort((a, b) => a - b);
}

/**
 * One metric for the selected year as a Jan–Dec bar chart, zero-filled. With a `compareYear` it
 * gains a second dataset for that year (primary first, so legend and auto-colours stay stable),
 * rendering as grouped bars like the Dashboard's current-vs-previous chart.
 */
export function buildMonthlyMetricChart(
  monthly: MonthlyAggregate[],
  year: number,
  metric: MonthlyMetric,
  months: readonly string[] = MONTH_LABELS,
  compareYear?: number | null,
): ChartData<'bar'> {
  const series = (of: number) => ({
    label: String(of),
    data: months.map((_, index) => monthly.find((m) => m.year === of && m.month === index + 1)?.[metric] ?? 0),
  });
  return {
    labels: [...months],
    datasets: compareYear == null ? [series(year)] : [series(year), series(compareYear)],
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

/** Total ride count per year across every year with data, as a single-series bar chart. */
export function buildRidesByYearChart(monthly: MonthlyAggregate[], label = 'rides'): ChartData<'bar'> {
  const years = statisticsYears(monthly);
  return {
    labels: years.map(String),
    datasets: [
      {
        label,
        data: years.map((year) => monthly.filter((m) => m.year === year).reduce((sum, m) => sum + m.rideCount, 0)),
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

/** Alpha suffix (0.4) making the comparison year's bars translucent while keeping their band colour. */
const COMPARE_ALPHA_HEX = '66';

function bandsForYear(yearly: YearlyTemperatureBand[], year: number): TemperatureBandSlice[] {
  return yearly
    .filter((band) => band.year === year)
    .map(({ fromCelsius, toCelsius, km }) => ({ fromCelsius, toCelsius, km }));
}

/**
 * Distance per 5°C band for one year, colour-coded cold to hot (Trends year-filtered chart). With a
 * `compareYear` both years are drawn as grouped bars, each keeping the cold→hot band colours, the
 * comparison year translucent so the two years stay distinguishable.
 */
export function buildYearTemperatureDistributionChart(
  yearly: YearlyTemperatureBand[],
  year: number,
  compareYear?: number | null,
): ChartData<'bar'> {
  const bands = bandsForYear(yearly, year);
  if (compareYear == null) {
    return buildTemperatureDistributionChart(bands);
  }

  const compareBands = bandsForYear(yearly, compareYear);
  // Both years share one band axis, so a band only one of them rode still lines up: take the union,
  // cold→hot, and read each year's distance off it (0 where that year has none).
  const axis = unionBands([...bands, ...compareBands]);
  const kmIn = (from: TemperatureBandSlice[], band: TemperatureBandSlice) =>
    from.find((b) => b.fromCelsius === band.fromCelsius && b.toCelsius === band.toCelsius)?.km ?? 0;

  return {
    labels: axis.map(bandLabel),
    datasets: [
      {
        label: String(year),
        data: axis.map((band) => kmIn(bands, band)),
        backgroundColor: axis.map(bandColor),
      },
      {
        label: String(compareYear),
        data: axis.map((band) => kmIn(compareBands, band)),
        backgroundColor: axis.map((band) => `${bandColor(band)}${COMPARE_ALPHA_HEX}`),
      },
    ],
  };
}

/** The distinct bands across the given slices, ordered cold to hot (the open-ended low band first). */
function unionBands(bands: TemperatureBandSlice[]): TemperatureBandSlice[] {
  const byRange = new Map<string, TemperatureBandSlice>();
  for (const band of bands) {
    byRange.set(`${band.fromCelsius}-${band.toCelsius}`, band);
  }
  return [...byRange.values()].sort((a, b) => (a.fromCelsius ?? -Infinity) - (b.fromCelsius ?? -Infinity));
}

/** Average ridden temperature per month as a line chart. */
export function buildTemperatureTrendChart(monthly: MonthlyTemperature[]): ChartData<'line'> {
  return {
    labels: monthly.map((m) => `${m.year}-${String(m.month).padStart(2, '0')}`),
    datasets: [{ label: '°C', data: monthly.map((m) => m.averageTemperatureCelsius) }],
  };
}
