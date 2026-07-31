import { describe, expect, it } from 'vitest';
import { WEATHER_AXIS_ID, headwindAlongSeries, summariseWeather, withHeadwindLayer } from './weather-layer';
import { buildMetricSeriesChart } from './metric-series-chart';
import type { MetricSample, WeatherHour } from '../../core/api/ride.models';

const at = (distanceKm: number, elapsedMinutes: number): MetricSample => ({ distanceKm, elapsedMinutes });

const hour = (iso: string, headwindKmh: number | null): WeatherHour => ({ hour: iso, headwindKmh });

const RIDE_START = '2026-06-01T08:00:00Z';

describe('headwindAlongSeries', () => {
  // Weather arrives by the hour while the series is sampled far more finely, so each sample takes
  // the value of the hour it fell inside. That is what lets one stepped line sit on the same x-axis
  // as the ride's own channels instead of needing a second chart.
  it('gives every sample the value of the hour it falls in', () => {
    const series = [at(0, 0), at(5, 30), at(11, 75), at(16, 110)];
    const weather = [hour('2026-06-01T08:00:00Z', 20), hour('2026-06-01T09:00:00Z', -8)];

    expect(headwindAlongSeries(series, weather, RIDE_START)).toEqual([20, 20, -8, -8]);
  });

  // A ride can run past the last hour the service reported, and an hour can report no wind at all.
  // Neither is worth inventing a number for — the line simply stops.
  it('leaves samples without a reported hour empty', () => {
    const series = [at(0, 0), at(20, 130)];
    const weather = [hour('2026-06-01T08:00:00Z', 12), hour('2026-06-01T09:00:00Z', null)];

    expect(headwindAlongSeries(series, weather, RIDE_START)).toEqual([12, null]);
  });

  it('has nothing to draw when no weather was stored', () => {
    expect(headwindAlongSeries([at(0, 0)], null, RIDE_START)).toBeNull();
  });
});

describe('withHeadwindLayer', () => {
  const series = [at(0, 0), at(5, 30), at(11, 75)];
  const weather = [hour('2026-06-01T08:00:00Z', 20), hour('2026-06-01T09:00:00Z', -8)];

  // The wind rides along beside the ride's own channels without joining them: its own axis, so it
  // cannot squash their scales, and stepped, because an hourly figure did not vary between readings.
  it('adds one dataset of its own without disturbing the channels', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation']);
    const before = chart.datasets.length;

    const withWind = withHeadwindLayer(chart, series, weather, '2026-06-01T08:00:00Z', 'Headwind');

    expect(withWind.datasets).toHaveLength(before + 1);
    const layer = withWind.datasets.at(-1)!;
    expect(layer.label).toBe('Headwind');
    expect(layer.yAxisID).toBe(WEATHER_AXIS_ID);
    expect(layer.stepped).toBe(true);
    expect(layer.data).toEqual([20, 20, -8]);
  });

  it('leaves the chart alone when there is no weather', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation']);

    expect(withHeadwindLayer(chart, series, null, '2026-06-01T08:00:00Z', 'Headwind')).toBe(chart);
  });
});

describe('summariseWeather', () => {
  const reading = (iso: string, windSpeedKmh: number, headwindKmh: number, temperatureCelsius: number, precipitationMm = 0): WeatherHour => ({
    hour: iso,
    windSpeedKmh,
    headwindKmh,
    temperatureCelsius,
    precipitationMm,
  });

  // The card answers "what was it like out there", so it wants ranges and one honest verdict on the
  // wind. The verdict is the mean of the signed headwind: a loop that fought its way out and was
  // pushed home nets out near zero, which is the truth about the ride even when one leg was brutal.
  it('reduces the hours to ranges and a signed wind verdict', () => {
    const summary = summariseWeather([
      reading('2026-06-01T08:00:00Z', 20, 18, 12, 0.4),
      reading('2026-06-01T09:00:00Z', 10, -6, 16, 0),
    ]);

    expect(summary).toEqual({
      windKmh: { minimum: 10, maximum: 20 },
      meanHeadwindKmh: 6,
      temperatureCelsius: { minimum: 12, maximum: 16 },
      precipitationMm: 0.4,
    });
  });

  it('has nothing to summarise without weather', () => {
    expect(summariseWeather(null)).toBeNull();
  });
});
