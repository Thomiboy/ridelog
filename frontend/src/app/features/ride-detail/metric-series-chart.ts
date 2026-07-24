import type { ChartData } from 'chart.js';
import type { MetricSample } from '../../core/api/ride.models';

export type MetricAxis = 'distance' | 'time';

/** Whether a series carries anything to plot (elevation or heart rate). */
export function hasGraphableSeries(series: MetricSample[]): boolean {
  return series.some((s) => s.elevationMeters != null || s.heartRate != null);
}

/**
 * Builds the elevation/HR line chart. The x-axis is the cumulative distance or the elapsed time
 * depending on `axis`; elevation and heart rate sit on separate y-axes and each dataset is dropped
 * when the series never recorded it.
 */
export function buildMetricSeriesChart(series: MetricSample[], axis: MetricAxis): ChartData<'line'> {
  const labels = series.map((s) => (axis === 'distance' ? s.distanceKm : s.elapsedMinutes));
  const datasets: ChartData<'line'>['datasets'] = [];

  if (series.some((s) => s.elevationMeters != null)) {
    datasets.push({
      label: 'Elevation',
      yAxisID: 'elevation',
      data: series.map((s) => s.elevationMeters ?? null),
      spanGaps: true,
    });
  }

  if (series.some((s) => s.heartRate != null)) {
    datasets.push({
      label: 'HR',
      yAxisID: 'hr',
      data: series.map((s) => s.heartRate ?? null),
      spanGaps: true,
    });
  }

  return { labels, datasets };
}
