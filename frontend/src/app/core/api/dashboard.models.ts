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

export interface MonthlyAverageTemperature {
  year: number;
  month: number;
  averageTemperatureCelsius?: number | null;
}

/**
 * The same calendar month one year ago (the whole month), mirroring the backend SameMonthLastYear.
 * Year and month are numbers so the label can name the month in the active language.
 */
export interface SameMonthLastYear {
  year: number;
  month: number;
  distanceKm: number;
  rideCount: number;
}

/** The dashboard aggregates, mirroring the backend DashboardStats DTO. */
export interface DashboardStats {
  thisMonth: PeriodStats;
  thisYear: PeriodStats;
  // Optional so the dashboard survives talking to an older backend that doesn't send these yet.
  lastYear?: PeriodStats;
  sameMonthLastYear?: SameMonthLastYear;
  monthlyDistance: MonthlyDistance[];
  averageSpeedTrend: MonthlySpeed[];
  // Optional so the dashboard survives an older backend that doesn't send it yet.
  averageTemperatureTrend?: MonthlyAverageTemperature[];
  /**
   * Whether this rider has ridden at all, ever. Everything else here covers this year and last, so
   * none of it can tell an empty log from one whose rides are simply older.
   */
  hasRides: boolean;
}
