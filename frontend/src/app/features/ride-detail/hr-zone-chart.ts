import type { ChartData } from 'chart.js';
import type { HrZoneSlice } from '../../core/api/ride.models';

/** Minutes spent per heart-rate zone as a Z1–Z5 bar chart. */
export function buildHrZoneChart(zones: HrZoneSlice[]): ChartData<'bar'> {
  return {
    labels: zones.map((z) => `Z${z.zone}`),
    datasets: [{ label: 'Minutes', data: zones.map((z) => Math.round(z.minutes * 10) / 10) }],
  };
}
