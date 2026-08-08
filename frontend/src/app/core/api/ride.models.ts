/** A page of results plus paging metadata, mirroring the backend PagedResult. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

/** Summary of a ride for list views. Mirrors the backend RideListItem DTO. */
/**
 * What a raw sport name reads as, decided by the API. Sent rather than worked out here, so the
 * reading of a sport lives in one place — two copies would drift into disagreeing about which list
 * a recording belongs to.
 */
export type SportCategory =
  | 'Cycling'
  | 'Running'
  | 'Walking'
  | 'Hiking'
  | 'Swimming'
  | 'Rowing'
  | 'Skiing'
  | 'Skating'
  | 'Other';

export interface RideSummary {
  id: string;
  startTime: string;
  distanceKm: number;
  durationMinutes: number;
  sport: string;
  sportCategory: SportCategory;
  averageSpeedKmh?: number;
  averageHeartRate?: number;
  elevationGainMeters?: number;
  /** Source chip tokens (PolarAutoSync / PolarImport / Bryton), localized in the UI. */
  sources: string[];
}

/** A long ride reduced to what the background map needs. Mirrors the backend LongestRideRoute DTO. */
export interface LongestRideRoute {
  id: string;
  date: string;
  distanceKm: number;
  routePolyline: string;
}

/** A ride's route for the all-routes coverage map. Mirrors the backend RideRoute DTO. */
export interface RideRoute {
  id: string;
  routePolyline: string;
}

/** One downsampled point of a ride's metric series, mirroring the backend MetricSample. */
export interface MetricSample {
  distanceKm: number;
  elapsedMinutes: number;
  elevationMeters?: number | null;
  heartRate?: number | null;
  temperatureCelsius?: number | null;
  /** The device's reading where the source had one, otherwise derived from distance over time. */
  speedKmh?: number | null;
}

/** Time spent in one HR zone (1–5), mirroring the backend HrZoneSlice. */
export interface HrZoneSlice {
  zone: number;
  minutes: number;
}

/** A place on the route where the rider paused for more than about a minute. */
export interface RestStop {
  latitude: number;
  longitude: number;
}

/**
 * One hour of weather reported for where and when a ride happened, mirroring the backend WeatherHour.
 * Reported for the area, not measured on the bike — which is why it never fills in for the ride's own
 * temperature. `headwindKmh` is positive into the wind and negative with it behind.
 */
export interface WeatherHour {
  hour: string;
  temperatureCelsius?: number | null;
  windSpeedKmh?: number | null;
  windFromBearing?: number | null;
  headwindKmh?: number | null;
  precipitationMm?: number | null;
  relativeHumidityPercent?: number | null;
  cloudCoverPercent?: number | null;
  weatherCode?: number | null;
}

/**
 * A ride's weather in the two shapes the detail view reads it: by the hour for the card, and
 * resolved against the direction ridden at every sample for the graph. Per sample because the wind
 * changes by the hour while the rider's direction changes with the road.
 */
export interface RideWeather {
  hours: WeatherHour[];
  /** Aligned one-for-one with the metric series; positive into the wind, negative with it behind. */
  headwindKmhBySample: (number | null)[];
}

/** Full ride detail, including the encoded route polyline for the map. */
export interface RideDetail extends RideSummary {
  endTime: string;
  maximumSpeedKmh?: number;
  maximumHeartRate?: number;
  averageCadence?: number;
  calories?: number;
  previousId?: string | null;
  nextId?: string | null;
  routePolyline?: string;
  metricSeries?: MetricSample[] | null;
  hrZones?: HrZoneSlice[] | null;
  restStops?: RestStop[];
  averageTemperatureCelsius?: number | null;
  minTemperatureCelsius?: number | null;
  maxTemperatureCelsius?: number | null;
  weather?: RideWeather | null;
}
