import {
  bandLabel,
  buildMonthlyMetricChart,
  buildRidesByYearChart,
  buildTemperatureDistributionChart,
  buildTemperatureTrendChart,
  buildYearTemperatureDistributionChart,
  buildYearTotalsChart,
  statisticsYears,
} from './statistics-charts';
import type {
  MonthlyAggregate,
  TemperatureBandSlice,
  MonthlyTemperature,
  YearlyTemperatureBand,
} from '../../core/api/statistics.models';
import { MONTH_LABELS } from '../../core/i18n/month-labels';

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

  it('uses the provided (localized) month labels on the x-axis', () => {
    const monthly = [aggregate(2026, 3, 100)];
    const hu = ['jan', 'febr', 'márc', 'ápr', 'máj', 'jún', 'júl', 'aug', 'szept', 'okt', 'nov', 'dec'];

    const chart = buildMonthlyMetricChart(monthly, 2026, 'distanceKm', hu);

    expect(chart.labels).toEqual(hu);
  });

  it('uses the provided (localized) rides legend label', () => {
    const chart = buildRidesByYearChart([aggregate(2026, 3, 100)], 'túrák');
    expect(chart.datasets[0].label).toBe('túrák');
  });

  it('adds a second zero-filled dataset for the comparison year, the primary year first', () => {
    const monthly = [aggregate(2026, 3, 100), aggregate(2025, 3, 60), aggregate(2025, 7, 80)];

    const chart = buildMonthlyMetricChart(monthly, 2026, 'distanceKm', MONTH_LABELS, 2025);

    expect(chart.datasets).toHaveLength(2);
    expect(chart.datasets[0].label).toBe('2026');
    expect(chart.datasets[1].label).toBe('2025');
    expect(chart.datasets[0].data[2]).toBe(100); // March 2026
    expect(chart.datasets[1].data[2]).toBe(60); // March 2025
    expect(chart.datasets[1].data[6]).toBe(80); // July 2025
    expect(chart.datasets[1].data[0]).toBe(0); // January 2025 had no rides
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

  it('builds a rides-by-year chart summing every month of each year', () => {
    const monthly = [
      { year: 2024, month: 5, distanceKm: 50, elevationGainMeters: 500, rideCount: 2, calories: 250 },
      { year: 2026, month: 3, distanceKm: 100, elevationGainMeters: 1000, rideCount: 4, calories: 500 },
      { year: 2026, month: 7, distanceKm: 40, elevationGainMeters: 400, rideCount: 3, calories: 200 },
    ];

    const chart = buildRidesByYearChart(monthly);

    // One bar per year with data, ascending; each bar is that year's summed ride count.
    expect(chart.labels).toEqual(['2024', '2026']);
    expect(chart.datasets[0].data).toEqual([2, 7]);
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

  it('builds a temperature distribution chart filtered to the selected year, in band order', () => {
    const yearly: YearlyTemperatureBand[] = [
      { year: 2025, fromCelsius: 0, toCelsius: 5, km: 10 },
      { year: 2026, fromCelsius: 0, toCelsius: 5, km: 3 },
      { year: 2026, fromCelsius: 5, toCelsius: 10, km: 40 },
    ];

    const chart = buildYearTemperatureDistributionChart(yearly, 2026);

    // Only 2026's bands, in band order, colour-coded like the all-time distribution.
    expect(chart.labels).toEqual(['0–5°', '5–10°']);
    expect(chart.datasets[0].data).toEqual([3, 40]);
    expect(chart.datasets[0].backgroundColor).toEqual(['#1e88e5', '#4fc3f7']);
  });

  it('compares two years as grouped band datasets, fading the comparison year', () => {
    const yearly: YearlyTemperatureBand[] = [
      { year: 2025, fromCelsius: 0, toCelsius: 5, km: 10 },
      { year: 2025, fromCelsius: 5, toCelsius: 10, km: 20 },
      { year: 2026, fromCelsius: 0, toCelsius: 5, km: 3 },
      { year: 2026, fromCelsius: 5, toCelsius: 10, km: 40 },
    ];

    const chart = buildYearTemperatureDistributionChart(yearly, 2026, 2025);

    expect(chart.labels).toEqual(['0–5°', '5–10°']);
    expect(chart.datasets).toHaveLength(2);
    expect(chart.datasets[0].label).toBe('2026');
    expect(chart.datasets[0].data).toEqual([3, 40]);
    expect(chart.datasets[1].label).toBe('2025');
    expect(chart.datasets[1].data).toEqual([10, 20]);
    // Both years keep the cold→hot band colours; the comparison year is faded so they stay distinct.
    expect(chart.datasets[0].backgroundColor).toEqual(['#1e88e5', '#4fc3f7']);
    expect(chart.datasets[1].backgroundColor).toEqual(['#1e88e566', '#4fc3f766']);
  });

  it('aligns both compared years on the union of their bands, zero-filling the gaps', () => {
    const yearly: YearlyTemperatureBand[] = [
      { year: 2026, fromCelsius: 0, toCelsius: 5, km: 3 },
      { year: 2025, fromCelsius: 5, toCelsius: 10, km: 20 },
      { year: 2025, fromCelsius: null, toCelsius: 0, km: 7 },
    ];

    const chart = buildYearTemperatureDistributionChart(yearly, 2026, 2025);

    // Every band either year rode, cold→hot; each year zero-filled where it has none, so the two
    // datasets line up under the same labels.
    expect(chart.labels).toEqual(['<0°', '0–5°', '5–10°']);
    expect(chart.datasets[0].data).toEqual([0, 3, 0]);
    expect(chart.datasets[1].data).toEqual([7, 0, 20]);
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
