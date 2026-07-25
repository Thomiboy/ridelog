import type { ChartData } from 'chart.js';
import type { MetricSample } from '../../core/api/ride.models';

export type MetricAxis = 'distance' | 'time';

/** Localized dataset labels for the elevation/HR/temperature lines. */
export interface MetricLabels {
  elevation: string;
  heartRate: string;
  temperature: string;
}

const DEFAULT_METRIC_LABELS: MetricLabels = { elevation: 'Elevation', heartRate: 'HR', temperature: 'Temperature' };

/** Whether a series carries anything to plot (elevation, heart rate or temperature). */
export function hasGraphableSeries(series: MetricSample[]): boolean {
  return series.some((s) => s.elevationMeters != null || s.heartRate != null || s.temperatureCelsius != null);
}

/**
 * Builds the elevation/HR line chart. The x-axis is the cumulative distance or the elapsed time
 * depending on `axis`; elevation and heart rate sit on separate y-axes and each dataset is dropped
 * when the series never recorded it.
 */
export function buildMetricSeriesChart(
  series: MetricSample[],
  axis: MetricAxis,
  labels: MetricLabels = DEFAULT_METRIC_LABELS,
): ChartData<'line'> {
  const x = series.map((s) => (axis === 'distance' ? s.distanceKm : s.elapsedMinutes));
  const datasets: ChartData<'line'>['datasets'] = [];

  if (series.some((s) => s.elevationMeters != null)) {
    datasets.push({
      label: labels.elevation,
      yAxisID: 'elevation',
      data: series.map((s) => s.elevationMeters ?? null),
      spanGaps: true,
    });
  }

  if (series.some((s) => s.heartRate != null)) {
    datasets.push({
      label: labels.heartRate,
      yAxisID: 'hr',
      data: series.map((s) => s.heartRate ?? null),
      spanGaps: true,
    });
  }

  if (series.some((s) => s.temperatureCelsius != null)) {
    datasets.push({
      label: labels.temperature,
      yAxisID: 'temperature',
      data: series.map((s) => s.temperatureCelsius ?? null),
      spanGaps: true,
    });
  }

  return { labels: x, datasets };
}

/**
 * Overlays two rides' elevation and heart-rate lines on a shared real-value x-axis (cumulative
 * distance or elapsed time), so rides of different length keep their own ranges. The compared ride's
 * lines are dashed to tell them apart; a channel a ride never recorded is dropped for that ride.
 */
export function buildComparisonMetricChart(
  current: MetricSample[],
  compare: MetricSample[],
  axis: MetricAxis,
  labels: MetricLabels = DEFAULT_METRIC_LABELS,
): ChartData<'line'> {
  const x = (s: MetricSample) => (axis === 'distance' ? s.distanceKm : s.elapsedMinutes);

  const linesFor = (series: MetricSample[], suffix: string, dashed: boolean): ChartData<'line'>['datasets'] => {
    const dash = dashed ? [6, 4] : undefined;
    const lines: ChartData<'line'>['datasets'] = [];
    if (series.some((s) => s.elevationMeters != null)) {
      lines.push({
        label: `${labels.elevation} ${suffix}`,
        yAxisID: 'elevation',
        borderDash: dash,
        data: series.map((s) => ({ x: x(s), y: s.elevationMeters ?? null })),
        spanGaps: true,
      });
    }
    if (series.some((s) => s.heartRate != null)) {
      lines.push({
        label: `${labels.heartRate} ${suffix}`,
        yAxisID: 'hr',
        borderDash: dash,
        data: series.map((s) => ({ x: x(s), y: s.heartRate ?? null })),
        spanGaps: true,
      });
    }
    return lines;
  };

  return { datasets: [...linesFor(current, 'A', false), ...linesFor(compare, 'B', true)] };
}
