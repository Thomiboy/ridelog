import { describe, expect, it } from 'vitest';
import { WEATHER_AXIS_ID, summariseWeather, withHeadwindLayer } from './weather-layer';
import { buildMetricSeriesChart } from './metric-series-chart';
import type { MetricSample, WeatherHour } from '../../core/api/ride.models';

const at = (distanceKm: number, elapsedMinutes: number): MetricSample => ({ distanceKm, elapsedMinutes });

describe('withHeadwindLayer', () => {
  const series = [at(0, 0), at(5, 30), at(11, 75)];

  // The wind rides along beside the ride's own channels without joining them, on an axis of its own
  // so it cannot squash their scales. The values arrive already resolved per sample, so the line can
  // change sign where the rider turned rather than only on the hour.
  it('adds one dataset of its own without disturbing the channels', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation']);
    const before = chart.datasets.length;

    const withWind = withHeadwindLayer(chart, [18, 2, -14], 'Headwind');

    expect(withWind.datasets).toHaveLength(before + 1);
    const layer = withWind.datasets.at(-1)!;
    expect(layer.label).toBe('Headwind');
    expect(layer.yAxisID).toBe(WEATHER_AXIS_ID);
    expect(layer.data).toEqual([18, 2, -14]);
  });

  it('leaves the chart alone when there is no weather', () => {
    const chart = buildMetricSeriesChart(series, 'distance', ['elevation']);

    expect(withHeadwindLayer(chart, null, 'Headwind')).toBe(chart);
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
