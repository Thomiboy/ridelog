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
}

/** Full ride detail, including the encoded route polyline for the map. */
export interface RideDetail extends RideSummary {
  endTime: string;
  source: string;
  maximumSpeedKmh?: number;
  maximumHeartRate?: number;
  averageCadence?: number;
  routePolyline?: string;
}
