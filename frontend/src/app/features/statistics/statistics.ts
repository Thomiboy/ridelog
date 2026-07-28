import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatCardModule } from '@angular/material/card';
import { StatisticsService } from '../../core/api/statistics.service';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { LanguageService } from '../../core/i18n/language.service';
import { monthLabels } from '../../core/i18n/month-labels';
import { DurationPipe } from '../../core/format/duration.pipe';
import type { StatisticsResult } from '../../core/api/statistics.models';
import { Chart } from '../../shared/chart/chart';
import {
  buildMonthlyMetricChart,
  buildRidesByYearChart,
  buildTemperatureDistributionChart,
  buildTemperatureTrendChart,
  buildYearTemperatureDistributionChart,
  buildYearTotalsChart,
  statisticsYears,
} from './statistics-charts';
import { buildHrZoneChart } from '../ride-detail/hr-zone-chart';

@Component({
  selector: 'app-statistics',
  imports: [Chart, RouterLink, TranslocoPipe, TranslocoDecimalPipe, TranslocoDatePipe, DurationPipe, MatCardModule],
  templateUrl: './statistics.html',
  styleUrl: './statistics.scss',
})
export class Statistics implements OnDestroy {
  private readonly statisticsService = inject(StatisticsService);
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  /** Localized short month names for chart axes; recomputes when the language changes. */
  private readonly months = computed(() => monthLabels(this.language.current()));

  /** How many of the longest routes the background map paints behind the charts. */
  private static readonly BackgroundRouteCount = 3;

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

  /** null = no comparison (the default on every visit; the choice isn't persisted). */
  private readonly compareYear = signal<number | null>(null);

  /** Years offered for comparison — never the primary year, so the same year can't be picked twice. */
  readonly compareYearOptions = computed(() => this.years().filter((year) => year !== this.activeYear()));

  /**
   * The comparison year actually in force: it drops back to "none" if the primary year is moved onto
   * it, or if the data no longer covers it.
   */
  readonly activeCompareYear = computed(() => {
    const picked = this.compareYear();
    return picked !== null && picked !== this.activeYear() && this.years().includes(picked) ? picked : null;
  });

  readonly distanceChart = this.metricChart('distanceKm');
  readonly elevationChart = this.metricChart('elevationGainMeters');
  readonly ridesChart = this.metricChart('rideCount');
  readonly caloriesChart = this.metricChart('calories');

  readonly yearTotalsChart = computed(() => {
    const stats = this.stats();
    return stats ? buildYearTotalsChart(stats.monthlyAggregates) : null;
  });

  readonly ridesByYearChart = computed(() => {
    const stats = this.stats();
    this.language.current(); // re-localize the legend when the language changes
    return stats ? buildRidesByYearChart(stats.monthlyAggregates, this.transloco.translate('statistics.trends.rides')) : null;
  });

  /** Distance by temperature for the selected year (Trends), unlike the all-time Temperature section. */
  readonly yearTemperatureDistributionChart = computed(() => {
    const temp = this.temperature();
    const year = this.activeYear();
    return temp && year !== null
      ? buildYearTemperatureDistributionChart(temp.yearlyDistribution, year, this.activeCompareYear())
      : null;
  });

  readonly records = computed(() => this.stats()?.records ?? null);

  readonly hrZoneChart = computed(() => {
    const zones = this.stats()?.hrZones;
    return zones && zones.some((z) => z.minutes > 0) ? buildHrZoneChart(zones) : null;
  });

  readonly temperature = computed(() => this.stats()?.temperature ?? null);

  readonly temperatureDistributionChart = computed(() => {
    const temp = this.temperature();
    return temp ? buildTemperatureDistributionChart(temp.distribution) : null;
  });

  readonly temperatureTrendChart = computed(() => {
    const temp = this.temperature();
    return temp && temp.monthlyAverage.length > 0 ? buildTemperatureTrendChart(temp.monthlyAverage) : null;
  });

  constructor() {
    this.statisticsService.getStatistics().subscribe((stats) => this.stats.set(stats));

    // The Statistics page paints its longest routes behind the charts; leaving restores the default.
    this.ridesService
      .getLongestRides(Statistics.BackgroundRouteCount)
      .subscribe((routes) => this.mapState.showRoutes(routes.map((route) => route.routePolyline)));
  }

  ngOnDestroy(): void {
    this.mapState.reset();
  }

  selectYear(value: string): void {
    this.selectedYear.set(Number(value));
  }

  /** The select's "none" option has an empty value, which clears the comparison. */
  selectCompareYear(value: string): void {
    this.compareYear.set(value === '' ? null : Number(value));
  }

  private metricChart(metric: Parameters<typeof buildMonthlyMetricChart>[2]) {
    return computed(() => {
      const stats = this.stats();
      const year = this.activeYear();
      return stats && year !== null
        ? buildMonthlyMetricChart(stats.monthlyAggregates, year, metric, this.months(), this.activeCompareYear())
        : null;
    });
  }
}
