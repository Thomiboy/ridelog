import { buildHrZoneChart } from './hr-zone-chart';
import type { HrZoneSlice } from '../../core/api/ride.models';

describe('hr zone chart', () => {
  const zones: HrZoneSlice[] = [
    { zone: 1, minutes: 0 },
    { zone: 2, minutes: 10 },
    { zone: 3, minutes: 25 },
    { zone: 4, minutes: 8 },
    { zone: 5, minutes: 0 },
  ];

  it('plots minutes per zone as a Z1–Z5 bar chart', () => {
    const chart = buildHrZoneChart(zones);

    expect(chart.labels).toEqual(['Z1', 'Z2', 'Z3', 'Z4', 'Z5']);
    expect(chart.datasets).toHaveLength(1);
    expect(chart.datasets[0].data).toEqual([0, 10, 25, 8, 0]);
  });

  it('colour-codes the bars from light blue (Z1) to red (Z5)', () => {
    const chart = buildHrZoneChart(zones);

    expect(chart.datasets[0].backgroundColor).toEqual([
      '#4fc3f7', // Z1 light blue
      '#1e88e5', // Z2 blue
      '#43a047', // Z3 green
      '#fbc02d', // Z4 yellow
      '#e53935', // Z5 red
    ]);
  });
});
