/** Totals for one calendar month of cycling, mirroring the backend MonthlyAggregate. */
export interface MonthlyAggregate {
  year: number;
  month: number;
  distanceKm: number;
  elevationGainMeters: number;
  rideCount: number;
  calories: number;
}

/** The single ride with the greatest distance. */
export interface LongestRideRecord {
  id: string;
  date: string;
  distanceKm: number;
}

/** The ride with the highest average speed among rides of at least 30 km. */
export interface FastestAverageRecord {
  id: string;
  date: string;
  averageSpeedKmh: number;
}

/** The longest run of consecutive calendar days that each had a ride. */
export interface StreakRecord {
  days: number;
  startDate: string;
  endDate: string;
}

/** Personal records for the Records section. */
export interface StatisticsRecords {
  longestRide?: LongestRideRecord | null;
  fastestAverage?: FastestAverageRecord | null;
  longestStreak?: StreakRecord | null;
}

/** The Statistics page feed, mirroring the backend StatisticsResult DTO. */
export interface StatisticsResult {
  monthlyAggregates: MonthlyAggregate[];
  records: StatisticsRecords;
}
