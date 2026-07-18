/** Aggregate totals for one period, mirroring the backend PeriodStats. */
export interface PeriodStats {
  distanceKm: number;
  rideCount: number;
  elevationGainMeters: number;
}

export interface MonthlyDistance {
  year: number;
  month: number;
  distanceKm: number;
}

export interface MonthlySpeed {
  year: number;
  month: number;
  averageSpeedKmh?: number | null;
}

/** The dashboard aggregates, mirroring the backend DashboardStats DTO. */
export interface DashboardStats {
  thisMonth: PeriodStats;
  thisYear: PeriodStats;
  monthlyDistance: MonthlyDistance[];
  averageSpeedTrend: MonthlySpeed[];
}
