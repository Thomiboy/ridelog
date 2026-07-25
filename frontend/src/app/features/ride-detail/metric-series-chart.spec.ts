import { buildComparisonMetricChart, buildMetricSeriesChart, hasGraphableSeries } from './metric-series-chart';
import type { MetricSample } from '../../core/api/ride.models';

const sample = (
  distanceKm: number,
  elapsedMinutes: number,
  elevationMeters?: number | null,
  heartRate?: number | null,
  temperatureCelsius?: number | null,
): MetricSample => ({
  distanceKm,
  elapsedMinutes,
  elevationMeters,
  heartRate,
  temperatureCelsius,
});

describe('metric series chart', () => {
  const series = [sample(0, 0, 100, 120), sample(1.5, 10, 150, 140), sample(3.0, 20, 120, 130)];

  it('labels the x-axis by distance when asked', () => {
    const chart = buildMetricSeriesChart(series, 'distance');
    expect(chart.labels).toEqual([0, 1.5, 3.0]);
  });

  it('labels the x-axis by elapsed time when asked', () => {
    const chart = buildMetricSeriesChart(series, 'time');
    expect(chart.labels).toEqual([0, 10, 20]);
  });

  it('plots elevation and heart rate as two datasets', () => {
    const chart = buildMetricSeriesChart(series, 'distance');

    const elevation = chart.datasets.find((d) => d.yAxisID === 'elevation')!;
    const hr = chart.datasets.find((d) => d.yAxisID === 'hr')!;
    expect(elevation.data).toEqual([100, 150, 120]);
    expect(hr.data).toEqual([120, 140, 130]);
  });

  it('omits a dataset the series never recorded', () => {
    const elevationOnly = [sample(0, 0, 100, null), sample(1, 5, 120, null)];
    const chart = buildMetricSeriesChart(elevationOnly, 'distance');

    expect(chart.datasets).toHaveLength(1);
    expect(chart.datasets[0].yAxisID).toBe('elevation');
  });

  it('plots temperature as its own dataset when present', () => {
    const withTemp = [sample(0, 0, 100, 120, 8), sample(1, 5, 120, 130, 12)];
    const chart = buildMetricSeriesChart(withTemp, 'distance');

    const temperature = chart.datasets.find((d) => d.yAxisID === 'temperature')!;
    expect(temperature).toBeTruthy();
    expect(temperature.data).toEqual([8, 12]);
  });

  it('overlays two rides as {x,y} points on a real distance axis', () => {
    const current = [sample(0, 0, 100, 120), sample(5, 10, 150, 150)];
    const compare = [sample(0, 0, 80, 110), sample(8, 12, 120, 140)];

    const chart = buildComparisonMetricChart(current, compare, 'distance');

    const elevations = chart.datasets.filter((d) => d.yAxisID === 'elevation');
    expect(elevations).toHaveLength(2);
    // Each ride keeps its own x range: current ends at 5 km, the compared ride at 8 km.
    expect((elevations[0].data as { x: number; y: number }[]).at(-1)).toEqual({ x: 5, y: 150 });
    expect((elevations[1].data as { x: number; y: number }[]).at(-1)).toEqual({ x: 8, y: 120 });
  });

  it('overlays the comparison on the elapsed-time axis when asked', () => {
    const current = [sample(0, 0, 100, 120), sample(5, 10, 150, 150)];
    const compare = [sample(0, 0, 80, 110), sample(8, 12, 120, 140)];

    const chart = buildComparisonMetricChart(current, compare, 'time');

    const elevations = chart.datasets.filter((d) => d.yAxisID === 'elevation');
    expect((elevations[0].data as { x: number; y: number }[]).at(-1)).toEqual({ x: 10, y: 150 });
    expect((elevations[1].data as { x: number; y: number }[]).at(-1)).toEqual({ x: 12, y: 120 });
  });

  it('drops a channel a ride never recorded from the overlay', () => {
    const current = [sample(0, 0, 100, 120), sample(5, 10, 150, 150)];
    const compareNoHr = [sample(0, 0, 80, null), sample(8, 12, 120, null)];

    const chart = buildComparisonMetricChart(current, compareNoHr, 'distance');

    // Only the current ride contributes a heart-rate line.
    expect(chart.datasets.filter((d) => d.yAxisID === 'hr')).toHaveLength(1);
  });

  it('reports whether a series has anything to graph', () => {
    expect(hasGraphableSeries([sample(0, 0, 100, null)])).toBe(true);
    expect(hasGraphableSeries([sample(0, 0, null, 120)])).toBe(true);
    expect(hasGraphableSeries([sample(0, 0, null, null, 15)])).toBe(true); // temperature-only
    expect(hasGraphableSeries([sample(0, 0, null, null)])).toBe(false);
    expect(hasGraphableSeries([])).toBe(false);
  });
});
