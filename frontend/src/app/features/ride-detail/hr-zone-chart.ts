import type { ChartData } from 'chart.js';
import type { HrZoneSlice } from '../../core/api/ride.models';

/** Zone colours, cool (easy) to warm (hard): Z1 light blue → Z5 red. */
const ZONE_COLORS: Record<number, string> = {
  1: '#4fc3f7', // light blue
  2: '#1e88e5', // blue
  3: '#43a047', // green
  4: '#fbc02d', // yellow
  5: '#e53935', // red
};

/** Minutes spent per heart-rate zone as a Z1–Z5 bar chart, colour-coded by zone. */
export function buildHrZoneChart(zones: HrZoneSlice[]): ChartData<'bar'> {
  return {
    labels: zones.map((z) => `Z${z.zone}`),
    datasets: [
      {
        label: 'Minutes',
        data: zones.map((z) => Math.round(z.minutes * 10) / 10),
        backgroundColor: zones.map((z) => ZONE_COLORS[z.zone] ?? '#9e9e9e'),
      },
    ],
  };
}
