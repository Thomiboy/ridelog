import { buildMonthlyDistanceChart, buildSpeedTrendChart } from './dashboard-charts';
import type { DashboardStats } from '../../core/api/dashboard.models';

describe('dashboard chart builders', () => {
  const stats: DashboardStats = {
    thisMonth: { distanceKm: 100, rideCount: 2, elevationGainMeters: 600 },
    thisYear: { distanceKm: 200, rideCount: 3, elevationGainMeters: 1100 },
    monthlyDistance: [
      // previous year: only July has distance
      ...Array.from({ length: 12 }, (_, i) => ({ year: 2025, month: i + 1, distanceKm: i + 1 === 7 ? 80 : 0 })),
      // current year: March and July
      ...Array.from({ length: 12 }, (_, i) => ({
        year: 2026,
        month: i + 1,
        distanceKm: i + 1 === 3 ? 100 : i + 1 === 7 ? 100 : 0,
      })),
    ],
    averageSpeedTrend: [
      { year: 2025, month: 8, averageSpeedKmh: null },
      { year: 2026, month: 3, averageSpeedKmh: 28 },
      { year: 2026, month: 7, averageSpeedKmh: 31 },
    ],
  };

  it('builds a two-series bar chart of monthly distance (current vs previous year)', () => {
    const chart = buildMonthlyDistanceChart(stats.monthlyDistance);

    expect(chart.labels!.length).toBe(12);
    expect(chart.labels![0]).toBe('Jan');
    expect(chart.datasets.length).toBe(2);

    const current = chart.datasets.find((d) => d.label === '2026')!;
    const previous = chart.datasets.find((d) => d.label === '2025')!;
    expect(current.data[2]).toBe(100); // March
    expect(current.data[6]).toBe(100); // July
    expect(current.data[0]).toBe(0);
    expect(previous.data[6]).toBe(80); // July last year
  });

  it('builds a line chart of the speed trend with month labels and gaps for empty months', () => {
    const chart = buildSpeedTrendChart(stats.averageSpeedTrend);

    expect(chart.labels).toEqual(['2025-08', '2026-03', '2026-07']);
    expect(chart.datasets.length).toBe(1);
    expect(chart.datasets[0].data).toEqual([null, 28, 31]);
  });
});
