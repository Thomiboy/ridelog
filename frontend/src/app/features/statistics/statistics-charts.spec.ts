import { buildMonthlyMetricChart, buildYearTotalsChart, statisticsYears } from './statistics-charts';
import type { MonthlyAggregate } from '../../core/api/statistics.models';

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
});
