import {
  bandLabel,
  buildMonthlyMetricChart,
  buildTemperatureDistributionChart,
  buildTemperatureTrendChart,
  buildYearTotalsChart,
  statisticsYears,
} from './statistics-charts';
import type { MonthlyAggregate, TemperatureBandSlice, MonthlyTemperature } from '../../core/api/statistics.models';

const aggregate = (year: number, month: number, distanceKm: number): MonthlyAggregate => ({
  year,
  month,
  distanceKm,
  elevationGainMeters: distanceKm * 10,
  rideCount: 1,
  calories: distanceKm * 5,
});

describe('statistics chart builders', () => {
  it('lists the distinct years with data, ascending', () => {
    const monthly = [aggregate(2026, 7, 100), aggregate(2024, 5, 50), aggregate(2026, 3, 80)];

    expect(statisticsYears(monthly)).toEqual([2024, 2026]);
  });

  it('builds a single-series Jan–Dec chart of the chosen metric for the selected year, zero-filled', () => {
    const monthly = [aggregate(2026, 3, 100), aggregate(2026, 7, 40), aggregate(2025, 7, 80)];

    const chart = buildMonthlyMetricChart(monthly, 2026, 'distanceKm');

    expect(chart.labels).toHaveLength(12);
    expect(chart.labels![0]).toBe('Jan');
    expect(chart.datasets).toHaveLength(1);
    expect(chart.datasets[0].label).toBe('2026');
    expect(chart.datasets[0].data[2]).toBe(100); // March 2026
    expect(chart.datasets[0].data[6]).toBe(40); // July 2026 (the 2025 July ride is excluded)
    expect(chart.datasets[0].data[0]).toBe(0); // January had no rides
  });

  it('reads whichever metric is asked for', () => {
    const monthly = [aggregate(2026, 3, 100)]; // elevation 1000, calories 500

    expect(buildMonthlyMetricChart(monthly, 2026, 'elevationGainMeters').datasets[0].data[2]).toBe(1000);
    expect(buildMonthlyMetricChart(monthly, 2026, 'calories').datasets[0].data[2]).toBe(500);
    expect(buildMonthlyMetricChart(monthly, 2026, 'rideCount').datasets[0].data[2]).toBe(1);
  });

  it('builds a year-over-year total-distance chart summing every month of each year', () => {
    const monthly = [aggregate(2024, 5, 50), aggregate(2026, 3, 100), aggregate(2026, 7, 40)];

    const chart = buildYearTotalsChart(monthly);

    // One bar per year with data, ascending; each bar is that year's summed distance.
    expect(chart.labels).toEqual(['2024', '2026']);
    expect(chart.datasets).toHaveLength(1);
    expect(chart.datasets[0].data).toEqual([50, 140]);
  });

  it('labels open-ended and inner temperature bands', () => {
    expect(bandLabel({ fromCelsius: null, toCelsius: 0, km: 0 })).toBe('<0°');
    expect(bandLabel({ fromCelsius: 25, toCelsius: null, km: 0 })).toBe('25°+');
    expect(bandLabel({ fromCelsius: 5, toCelsius: 10, km: 0 })).toBe('5–10°');
  });

  it('builds a temperature distribution bar chart labelled by band', () => {
    const bands: TemperatureBandSlice[] = [
      { fromCelsius: null, toCelsius: 0, km: 3 },
      { fromCelsius: 0, toCelsius: 5, km: 12 },
      { fromCelsius: 5, toCelsius: 10, km: 40 },
    ];

    const chart = buildTemperatureDistributionChart(bands);

    expect(chart.labels).toEqual(['<0°', '0–5°', '5–10°']);
    expect(chart.datasets[0].data).toEqual([3, 12, 40]);
  });

  it('colour-codes the temperature bands by their range, cold to hot', () => {
    const bands: TemperatureBandSlice[] = [
      { fromCelsius: null, toCelsius: 0, km: 0 }, // below 0 → deep blue
      { fromCelsius: 20, toCelsius: 25, km: 0 }, // 20–25 → orange
      { fromCelsius: 25, toCelsius: null, km: 0 }, // 25+ → red
    ];

    const colors = buildTemperatureDistributionChart(bands).datasets[0].backgroundColor;
    expect(colors).toEqual(['#0d47a1', '#fb8c00', '#e53935']);
  });

  it('builds a monthly average-temperature line chart', () => {
    const monthly: MonthlyTemperature[] = [
      { year: 2026, month: 3, averageTemperatureCelsius: 8 },
      { year: 2026, month: 7, averageTemperatureCelsius: 21 },
    ];

    const chart = buildTemperatureTrendChart(monthly);

    expect(chart.labels).toEqual(['2026-03', '2026-07']);
    expect(chart.datasets[0].data).toEqual([8, 21]);
  });
});
