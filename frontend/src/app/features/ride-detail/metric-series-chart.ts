import type { ChartData } from 'chart.js';
import type { MetricSample } from '../../core/api/ride.models';

export type MetricAxis = 'distance' | 'time';

/** The metrics the ride-detail graph can plot; the user picks up to two at a time. */
export type MetricChannel = 'elevation' | 'heartRate' | 'temperature' | 'speed';

/** Every channel, in the order the picker offers them. */
export const METRIC_CHANNELS: readonly MetricChannel[] = ['elevation', 'heartRate', 'temperature', 'speed'];

/** Localized dataset labels, one per channel. */
export type MetricLabels = Record<MetricChannel, string>;

const DEFAULT_METRIC_LABELS: MetricLabels = {
  elevation: 'Elevation',
  heartRate: 'HR',
  temperature: 'Temperature',
  speed: 'Speed',
};

/** How each channel is read off a sample and which y-axis it owns. */
const CHANNELS: Record<MetricChannel, { axisId: string; value: (s: MetricSample) => number | null | undefined }> = {
  elevation: { axisId: 'elevation', value: (s) => s.elevationMeters },
  heartRate: { axisId: 'hr', value: (s) => s.heartRate },
  temperature: { axisId: 'temperature', value: (s) => s.temperatureCelsius },
  speed: { axisId: 'speed', value: (s) => s.speedKmh },
};

/** The y-axis a channel is plotted against, so the caller can configure exactly the axes in use. */
export function channelAxisId(channel: MetricChannel): string {
  return CHANNELS[channel].axisId;
}

/** Whether the series recorded the channel anywhere — an empty channel isn't worth offering. */
export function seriesHasChannel(series: MetricSample[], channel: MetricChannel): boolean {
  return series.some((s) => CHANNELS[channel].value(s) != null);
}

/** The channels this series can actually plot, in picker order. */
export function availableChannels(series: MetricSample[]): MetricChannel[] {
  return METRIC_CHANNELS.filter((channel) => seriesHasChannel(series, channel));
}

/** Whether a series carries anything to plot at all. */
export function hasGraphableSeries(series: MetricSample[]): boolean {
  return availableChannels(series).length > 0;
}

/** How many channels share the plot: two y-axes, so two lines. */
const MAX_SHOWN_CHANNELS = 2;

/** The pair the graph opens on, in preference order; missing ones fall back to whatever was recorded. */
const PREFERRED_CHANNELS: readonly MetricChannel[] = ['heartRate', 'speed'];

/**
 * Turns a channel on or off within the two-line budget: picking a third drops the one selected
 * longest ago, so switching is a single click. The last remaining channel can't be turned off —
 * an empty plot is never what you meant.
 */
export function toggleChannel(selected: readonly MetricChannel[], channel: MetricChannel): MetricChannel[] {
  if (selected.includes(channel)) {
    return selected.length > 1 ? selected.filter((c) => c !== channel) : [...selected];
  }
  return [...selected.slice(Math.max(0, selected.length - (MAX_SHOWN_CHANNELS - 1))), channel];
}

/**
 * The channels to show when a ride opens: heart rate and speed where the ride has them, otherwise
 * the next channels it did record, so the graph is never blank.
 */
export function defaultChannels(available: readonly MetricChannel[]): MetricChannel[] {
  const preferred = PREFERRED_CHANNELS.filter((channel) => available.includes(channel));
  const fillers = available.filter((channel) => !preferred.includes(channel));
  return [...preferred, ...fillers].slice(0, MAX_SHOWN_CHANNELS);
}

/**
 * Builds the metric line chart for the chosen channels. The x-axis is the cumulative distance or the
 * elapsed time depending on `axis`; each channel sits on its own y-axis, in the order given, so the
 * caller can configure exactly those two axes. A channel the series never recorded is dropped.
 */
export function buildMetricSeriesChart(
  series: MetricSample[],
  axis: MetricAxis,
  channels: readonly MetricChannel[],
  labels: MetricLabels = DEFAULT_METRIC_LABELS,
): ChartData<'line'> {
  const x = series.map((s) => (axis === 'distance' ? s.distanceKm : s.elapsedMinutes));

  return {
    labels: x,
    datasets: channels
      .filter((channel) => seriesHasChannel(series, channel))
      .map((channel) => ({
        label: labels[channel],
        yAxisID: CHANNELS[channel].axisId,
        data: series.map((s) => CHANNELS[channel].value(s) ?? null),
        spanGaps: true,
      })),
  };
}

/**
 * Overlays two rides' lines for the chosen channels on a shared real-value x-axis (cumulative
 * distance or elapsed time), so rides of different length keep their own ranges. The compared ride's
 * lines are dashed to tell them apart; a channel a ride never recorded is dropped for that ride.
 */
export function buildComparisonMetricChart(
  current: MetricSample[],
  compare: MetricSample[],
  axis: MetricAxis,
  channels: readonly MetricChannel[] = ['elevation', 'heartRate'],
  labels: MetricLabels = DEFAULT_METRIC_LABELS,
): ChartData<'line'> {
  const x = (s: MetricSample) => (axis === 'distance' ? s.distanceKm : s.elapsedMinutes);

  const linesFor = (series: MetricSample[], suffix: string, dashed: boolean): ChartData<'line'>['datasets'] =>
    channels
      .filter((channel) => seriesHasChannel(series, channel))
      .map((channel) => ({
        label: `${labels[channel]} ${suffix}`,
        yAxisID: CHANNELS[channel].axisId,
        borderDash: dashed ? [6, 4] : undefined,
        data: series.map((s) => ({ x: x(s), y: CHANNELS[channel].value(s) ?? null })),
        spanGaps: true,
      }));

  return { datasets: [...linesFor(current, 'A', false), ...linesFor(compare, 'B', true)] };
}
