/** A page of results plus paging metadata, mirroring the backend PagedResult. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

/** Summary of a ride for list views. Mirrors the backend RideListItem DTO. */
export interface RideSummary {
  id: string;
  startTime: string;
  distanceKm: number;
  durationMinutes: number;
  sport: string;
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
}
