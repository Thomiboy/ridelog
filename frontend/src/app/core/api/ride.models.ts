/** Summary of a ride for list views. Mirrors the backend read DTO (added in a later issue). */
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
  maximumSpeedKmh?: number;
  maximumHeartRate?: number;
  averageCadence?: number;
  routePolyline?: string;
}
