import {
  availableChannels,
  sampleIndexAtX,
  buildComparisonMetricChart,
  buildMetricSeriesChart,
  defaultChannels,
  hasGraphableSeries,
  toggleChannel,
} from './metric-series-chart';
import type { MetricSample } from '../../core/api/ride.models';

const sample = (
  distanceKm: number,
  elapsedMinutes: number,
  elevationMeters?: number | null,
  heartRate?: number | null,
  temperatureCelsius?: number | null,
  speedKmh?: number | null,
): MetricSample => ({
  distanceKm,
  elapsedMinutes,
  elevationMeters,
  heartRate,
  temperatureCelsius,
  speedKmh,
});

describe('metric series chart', () => {
  const series = [sample(0, 0, 100, 120), sample(1.5, 10, 150, 140), sample(3.0, 20, 120, 130)];

  it('labels the x-axis by distance when asked', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation', 'heartRate']);
    expect(chart.labels).toEqual([0, 1.5, 3.0]);
  });

  it('labels the x-axis by elapsed time when asked', () => {
    const chart = buildMetricSeriesChart(series, 'time', ['elevation', 'heartRate']);
    expect(chart.labels).toEqual([0, 10, 20]);
  });

  it('plots elevation and heart rate as two datasets', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation', 'heartRate']);

    const elevation = chart.datasets.find((d) => d.yAxisID === 'elevation')!;
    const hr = chart.datasets.find((d) => d.yAxisID === 'hr')!;
    expect(elevation.data).toEqual([100, 150, 120]);
    expect(hr.data).toEqual([120, 140, 130]);
  });

  it('uses the provided (localized) dataset labels', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation', 'heartRate'], {
      elevation: 'Szint',
      heartRate: 'Pulzus',
      temperature: 'Hő',
      speed: 'Sebesség',
    });

    expect(chart.datasets.find((d) => d.yAxisID === 'elevation')!.label).toBe('Szint');
    expect(chart.datasets.find((d) => d.yAxisID === 'hr')!.label).toBe('Pulzus');
  });

  it('omits a dataset the series never recorded', () => {
    const elevationOnly = [sample(0, 0, 100, null), sample(1, 5, 120, null)];
    const chart = buildMetricSeriesChart(elevationOnly, 'distance', ['elevation', 'heartRate']);

    expect(chart.datasets).toHaveLength(1);
    expect(chart.datasets[0].yAxisID).toBe('elevation');
  });

  it('plots only the channels asked for, each on its own axis, in the order given', () => {
    const withAll = [sample(0, 0, 100, 120, 8, 22), sample(1, 5, 120, 130, 12, 25)];

    const chart = buildMetricSeriesChart(withAll, 'distance', ['heartRate', 'speed']);

    expect(chart.datasets.map((d) => d.yAxisID)).toEqual(['hr', 'speed']);
    expect(chart.datasets.find((d) => d.yAxisID === 'speed')!.data).toEqual([22, 25]);
    expect(chart.datasets.find((d) => d.yAxisID === 'hr')!.data).toEqual([120, 130]);
  });

  it('plots temperature as its own dataset when present', () => {
    const withTemp = [sample(0, 0, 100, 120, 8), sample(1, 5, 120, 130, 12)];
    const chart = buildMetricSeriesChart(withTemp, 'distance', ['temperature']);

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

  it('overlays only the chosen channels for both rides, dashing the compared one', () => {
    const current = [sample(0, 0, 100, 120, null, 22), sample(5, 10, 150, 150, null, 28)];
    const compare = [sample(0, 0, 80, 110, null, 20), sample(8, 12, 120, 140, null, 26)];

    const chart = buildComparisonMetricChart(current, compare, 'distance', ['speed']);

    const speeds = chart.datasets.filter((d) => d.yAxisID === 'speed');
    expect(speeds).toHaveLength(2); // one line per ride
    expect(chart.datasets.filter((d) => d.yAxisID === 'elevation')).toHaveLength(0);
    expect(speeds[0].borderDash).toBeUndefined(); // this ride solid
    expect(speeds[1].borderDash).toEqual([6, 4]); // the compared ride dashed
  });

  it('drops a channel a ride never recorded from the overlay', () => {
    const current = [sample(0, 0, 100, 120), sample(5, 10, 150, 150)];
    const compareNoHr = [sample(0, 0, 80, null), sample(8, 12, 120, null)];

    const chart = buildComparisonMetricChart(current, compareNoHr, 'distance');

    // Only the current ride contributes a heart-rate line.
    expect(chart.datasets.filter((d) => d.yAxisID === 'hr')).toHaveLength(1);
  });

  it('picks a second channel, then swaps out the oldest rather than crowding the plot', () => {
    expect(toggleChannel(['elevation'], 'speed')).toEqual(['elevation', 'speed']);
    expect(toggleChannel(['elevation', 'heartRate'], 'speed')).toEqual(['heartRate', 'speed']);
  });

  it('deselects a shown channel but never the last one, so the plot is never empty', () => {
    expect(toggleChannel(['elevation', 'heartRate'], 'elevation')).toEqual(['heartRate']);
    expect(toggleChannel(['heartRate'], 'heartRate')).toEqual(['heartRate']);
  });

  it('opens on heart rate and speed, falling back to what the ride recorded', () => {
    expect(defaultChannels(['elevation', 'heartRate', 'temperature', 'speed'])).toEqual(['heartRate', 'speed']);
    // No heart rate on this ride: speed leads and the next available channel fills the second slot.
    expect(defaultChannels(['elevation', 'speed'])).toEqual(['speed', 'elevation']);
    expect(defaultChannels(['elevation'])).toEqual(['elevation']);
    expect(defaultChannels([])).toEqual([]);
  });

  it('lists the channels a series actually recorded, in picker order', () => {
    const noTemp = [sample(0, 0, 100, 120, null, 22), sample(1, 5, 120, 130, null, 25)];

    expect(availableChannels(noTemp)).toEqual(['elevation', 'heartRate', 'speed']);
  });

  it('reports whether a series has anything to graph', () => {
    expect(hasGraphableSeries([sample(0, 0, 100, null)])).toBe(true);
    expect(hasGraphableSeries([sample(0, 0, null, 120)])).toBe(true);
    expect(hasGraphableSeries([sample(0, 0, null, null, 15)])).toBe(true); // temperature-only
    expect(hasGraphableSeries([sample(0, 0, null, null)])).toBe(false);
    expect(hasGraphableSeries([])).toBe(false);
  });
});

describe('sampleIndexAtX', () => {
  // The two directions share one coordinate — the x on the axis the reader is looking at — so both
  // need to turn an x back into a point of the series. Nearest wins: the series is downsampled, so
  // an exact match is the exception rather than the rule.
  const series = [sample(0, 0), sample(5, 30), sample(11, 75)];

  it('finds the sample nearest an x on the distance axis', () => {
    expect(sampleIndexAtX(series, 6, 'distance')).toBe(1);
  });

  it('finds the sample nearest an x on the time axis', () => {
    // 60 minutes is 30 past the second sample but only 15 short of the third.
    expect(sampleIndexAtX(series, 60, 'time')).toBe(2);
  });

  it('clamps to the ends rather than answering with nothing', () => {
    expect(sampleIndexAtX(series, -100, 'distance')).toBe(0);
    expect(sampleIndexAtX(series, 900, 'distance')).toBe(2);
  });
});
