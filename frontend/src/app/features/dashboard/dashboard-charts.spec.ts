import { buildMonthlyDistanceChart, buildSpeedAndTemperatureTrendChart } from './dashboard-charts';
import type { DashboardStats } from '../../core/api/dashboard.models';

describe('dashboard chart builders', () => {
  const stats: DashboardStats = {
    hasRides: true,
    thisMonth: { distanceKm: 100, rideCount: 2, elevationGainMeters: 600 },
    thisYear: { distanceKm: 200, rideCount: 3, elevationGainMeters: 1100 },
    lastYear: { distanceKm: 80, rideCount: 1, elevationGainMeters: 300 },
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
    averageTemperatureTrend: [
      { year: 2025, month: 8, averageTemperatureCelsius: null },
      { year: 2026, month: 3, averageTemperatureCelsius: 12 },
      { year: 2026, month: 7, averageTemperatureCelsius: 22 },
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

  it('builds a dual-axis speed + temperature line chart with aligned labels and gaps', () => {
    const chart = buildSpeedAndTemperatureTrendChart(stats.averageSpeedTrend, stats.averageTemperatureTrend!);

    expect(chart.labels).toEqual(['2025-08', '2026-03', '2026-07']);
    expect(chart.datasets.length).toBe(2);

    const speed = chart.datasets.find((d) => d.yAxisID === 'speed')!;
    const temperature = chart.datasets.find((d) => d.yAxisID === 'temp')!;
    expect(speed.data).toEqual([null, 28, 31]);
    expect(temperature.data).toEqual([null, 12, 22]);
    // Empty months stay as gaps on both lines.
    expect(speed.spanGaps).toBe(false);
    expect(temperature.spanGaps).toBe(false);
  });
});
