import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatCardModule } from '@angular/material/card';
import { StatisticsService } from '../../core/api/statistics.service';
import type { StatisticsResult } from '../../core/api/statistics.models';
import { Chart } from '../../shared/chart/chart';
import { buildMonthlyMetricChart, buildYearTotalsChart, statisticsYears } from './statistics-charts';
import { buildHrZoneChart } from '../ride-detail/hr-zone-chart';

@Component({
  selector: 'app-statistics',
  imports: [Chart, RouterLink, TranslocoPipe, DecimalPipe, DatePipe, MatCardModule],
  templateUrl: './statistics.html',
  styleUrl: './statistics.scss',
})
export class Statistics {
  private readonly statisticsService = inject(StatisticsService);

  readonly stats = signal<StatisticsResult | null>(null);

  /** null until the user picks; the active year then falls back to the latest year with data. */
  private readonly selectedYear = signal<number | null>(null);

  readonly years = computed(() => statisticsYears(this.stats()?.monthlyAggregates ?? []));

  readonly activeYear = computed(() => {
    const years = this.years();
    const picked = this.selectedYear();
    if (picked !== null && years.includes(picked)) {
      return picked;
    }
    // Default = the most recent year with data (the current year whenever it has rides).
    return years.at(-1) ?? null;
  });

  readonly distanceChart = this.metricChart('distanceKm');
  readonly elevationChart = this.metricChart('elevationGainMeters');
  readonly ridesChart = this.metricChart('rideCount');
  readonly caloriesChart = this.metricChart('calories');

  readonly yearTotalsChart = computed(() => {
    const stats = this.stats();
    return stats ? buildYearTotalsChart(stats.monthlyAggregates) : null;
  });

  readonly records = computed(() => this.stats()?.records ?? null);

  readonly hrZoneChart = computed(() => {
    const zones = this.stats()?.hrZones;
    return zones && zones.some((z) => z.minutes > 0) ? buildHrZoneChart(zones) : null;
  });

  constructor() {
    this.statisticsService.getStatistics().subscribe((stats) => this.stats.set(stats));
  }

  selectYear(value: string): void {
    this.selectedYear.set(Number(value));
  }

  private metricChart(metric: Parameters<typeof buildMonthlyMetricChart>[2]) {
    return computed(() => {
      const stats = this.stats();
      const year = this.activeYear();
      return stats && year !== null ? buildMonthlyMetricChart(stats.monthlyAggregates, year, metric) : null;
    });
  }
}
