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

/** One downsampled point of a ride's metric series, mirroring the backend MetricSample. */
export interface MetricSample {
  distanceKm: number;
  elapsedMinutes: number;
  elevationMeters?: number | null;
  heartRate?: number | null;
}

/** Time spent in one HR zone (1–5), mirroring the backend HrZoneSlice. */
export interface HrZoneSlice {
  zone: number;
  minutes: number;
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
}
