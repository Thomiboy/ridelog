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
  /** Moving time ridden that month, in minutes; the time chart renders it as hours. */
  durationMinutes: number;
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
  /** Distance ridden across the streak's days — also what breaks ties between equal-length streaks. */
  distanceKm: number;
}

/** The single ride that burned the most calories. */
export interface MostCaloriesRecord {
  id: string;
  date: string;
  calories: number;
}

/** The single ride with the greatest moving duration (minutes). */
export interface LongestDurationRecord {
  id: string;
  date: string;
  durationMinutes: number;
}

/**
 * The best calendar month for a metric. Year and month are numbers so the month name can be
 * formatted in the active language rather than fixed by the backend.
 */
export interface BestMonthDistanceRecord {
  year: number;
  month: number;
  distanceKm: number;
}

/** The calendar month with the most rides; year and month are numbers, as above. */
export interface BestMonthRidesRecord {
  year: number;
  month: number;
  rideCount: number;
}

/** The single ride that reached the highest speed. */
export interface MaxSpeedRecord {
  id: string;
  date: string;
  maxSpeedKmh: number;
}

/** The single ride with the greatest elevation gain. */
export interface BiggestClimbRecord {
  id: string;
  date: string;
  elevationGainMeters: number;
}

/** Personal records for the Records section. */
export interface StatisticsRecords {
  longestRide?: LongestRideRecord | null;
  fastestAverage?: FastestAverageRecord | null;
  longestStreak?: StreakRecord | null;
  mostCalories?: MostCaloriesRecord | null;
  longestDuration?: LongestDurationRecord | null;
  bestMonthDistance?: BestMonthDistanceRecord | null;
  bestMonthRides?: BestMonthRidesRecord | null;
  maxSpeed?: MaxSpeedRecord | null;
  biggestClimb?: BiggestClimbRecord | null;
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

/** Distance ridden in one 5°C band within one year, for the Trends year-filtered chart. */
export interface YearlyTemperatureBand {
  year: number;
  fromCelsius: number | null;
  toCelsius: number | null;
  km: number;
}

/** The Statistics page's Temperature section. */
export interface TemperatureStats {
  distribution: TemperatureBandSlice[];
  coldest: TemperatureExtreme | null;
  warmest: TemperatureExtreme | null;
  seasonMinCelsius: number | null;
  seasonMaxCelsius: number | null;
  monthlyAverage: MonthlyTemperature[];
  yearlyDistribution: YearlyTemperatureBand[];
}

/** The Statistics page feed, mirroring the backend StatisticsResult DTO. */
export interface StatisticsResult {
  monthlyAggregates: MonthlyAggregate[];
  records: StatisticsRecords;
  hrZones?: HrZoneSlice[] | null;
  temperature?: TemperatureStats | null;
}
