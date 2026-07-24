import type { HrZoneSlice } from './ride.models';

export type { HrZoneSlice };

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

/** Distance ridden in one 5°C temperature band; open-ended bounds are null. */
export interface TemperatureBandSlice {
  fromCelsius: number | null;
  toCelsius: number | null;
  km: number;
}

/** The coldest or warmest ride by average temperature. */
export interface TemperatureExtreme {
  id: string;
  date: string;
  averageTemperatureCelsius: number;
}

/** Average ridden temperature in one calendar month. */
export interface MonthlyTemperature {
  year: number;
  month: number;
  averageTemperatureCelsius: number;
}

/** The Statistics page's Temperature section. */
export interface TemperatureStats {
  distribution: TemperatureBandSlice[];
  coldest: TemperatureExtreme | null;
  warmest: TemperatureExtreme | null;
  seasonMinCelsius: number | null;
  seasonMaxCelsius: number | null;
  monthlyAverage: MonthlyTemperature[];
}

/** The Statistics page feed, mirroring the backend StatisticsResult DTO. */
export interface StatisticsResult {
  monthlyAggregates: MonthlyAggregate[];
  records: StatisticsRecords;
  hrZones?: HrZoneSlice[] | null;
  temperature?: TemperatureStats | null;
}
