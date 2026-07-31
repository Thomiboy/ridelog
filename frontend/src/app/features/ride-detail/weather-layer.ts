import type { ChartData } from 'chart.js';
import type { MetricSample, WeatherHour } from '../../core/api/ride.models';

/** The y-axis the wind owns, so it can never rescale the ride's own channels. */
export const WEATHER_AXIS_ID = 'wind';

const MINUTES_PER_HOUR = 60;

/**
 * Spreads the hourly headwind across the ride's samples, so it can be drawn as one stepped line on
 * the same x-axis as the ride's own channels rather than in a chart of its own.
 *
 * Weather is not a channel — nothing measured it on the bike — so it never joins the channel picker.
 * This is only about getting an hourly figure onto a per-sample axis.
 *
 * Returns null when there is no weather at all, so the caller can leave the layer off entirely.
 */
export function headwindAlongSeries(
  series: readonly MetricSample[],
  weather: readonly WeatherHour[] | null | undefined,
  rideStart: string,
): (number | null)[] | null {
  if (!weather?.length) {
    return null;
  }

  const startMs = Date.parse(rideStart);
  const minutesInto = weather.map((reading) => (Date.parse(reading.hour) - startMs) / 60_000);

  return series.map((sample) => {
    const index = minutesInto.findIndex(
      (start) => sample.elapsedMinutes >= start && sample.elapsedMinutes < start + MINUTES_PER_HOUR,
    );
    return index === -1 ? null : (weather[index].headwindKmh ?? null);
  });
}

/**
 * Draws the hourly headwind alongside the ride's channels as one stepped line on its own axis.
 * Stepped because the figure is hourly and did not vary between readings; on its own axis because a
 * wind in km/h next to a heart rate would flatten one of them.
 *
 * Returns the chart untouched when there is no weather, so the caller has nothing to special-case.
 */
export function withHeadwindLayer(
  chart: ChartData<'line'>,
  series: readonly MetricSample[],
  weather: readonly WeatherHour[] | null | undefined,
  rideStart: string,
  label: string,
): ChartData<'line'> {
  const data = headwindAlongSeries(series, weather, rideStart);
  if (!data) {
    return chart;
  }

  return {
    ...chart,
    datasets: [
      ...chart.datasets,
      {
        label,
        yAxisID: WEATHER_AXIS_ID,
        data,
        stepped: true,
        borderDash: [2, 3],
        pointRadius: 0,
        fill: false,
        spanGaps: false,
      },
    ],
  };
}

/** A low and a high that always travel together — either the hours reported it or they did not. */
export interface WeatherRange {
  minimum: number;
  maximum: number;
}

/** What the weather card reads off a ride's hours: ranges, plus one verdict on the wind. */
export interface WeatherSummary {
  windKmh: WeatherRange | null;
  /** Mean signed headwind: positive means the ride was, on balance, into it. */
  meanHeadwindKmh: number | null;
  temperatureCelsius: WeatherRange | null;
  precipitationMm: number | null;
}

/**
 * Reduces a ride's hours to what a card can hold. The wind verdict is the mean of the signed
 * headwind rather than of the wind speed: a loop that fights its way out and is pushed home nets out
 * near zero, and that is the honest answer even when one leg was brutal.
 */
export function summariseWeather(weather: readonly WeatherHour[] | null | undefined): WeatherSummary | null {
  if (!weather?.length) {
    return null;
  }

  const values = (pick: (hour: WeatherHour) => number | null | undefined): number[] =>
    weather.map(pick).filter((value): value is number => value != null);

  const winds = values((hour) => hour.windSpeedKmh);
  const headwinds = values((hour) => hour.headwindKmh);
  const temperatures = values((hour) => hour.temperatureCelsius);
  const rain = values((hour) => hour.precipitationMm);

  const mean = (list: number[]) => list.reduce((total, value) => total + value, 0) / list.length;

  const range = (list: number[]): WeatherRange | null =>
    list.length ? { minimum: Math.min(...list), maximum: Math.max(...list) } : null;

  return {
    windKmh: range(winds),
    meanHeadwindKmh: headwinds.length ? mean(headwinds) : null,
    temperatureCelsius: range(temperatures),
    precipitationMm: rain.length ? rain.reduce((total, value) => total + value, 0) : null,
  };
}
