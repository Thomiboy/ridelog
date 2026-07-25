import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatCardModule } from '@angular/material/card';
import { DashboardService } from '../../core/api/dashboard.service';
import type { DashboardStats } from '../../core/api/dashboard.models';
import { Chart } from '../../shared/chart/chart';
import {
  buildMonthlyDistanceChart,
  buildSpeedAndTemperatureTrendChart,
  MONTH_LABELS,
  SPEED_TEMPERATURE_TREND_OPTIONS,
} from './dashboard-charts';

@Component({
  selector: 'app-dashboard',
  imports: [Chart, TranslocoPipe, DecimalPipe, MatCardModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly dashboardService = inject(DashboardService);

  readonly stats = signal<DashboardStats | null>(null);

  readonly distanceChart = computed(() => {
    const stats = this.stats();
    return stats ? buildMonthlyDistanceChart(stats.monthlyDistance) : null;
  });

  readonly speedChart = computed(() => {
    const stats = this.stats();
    return stats
      ? buildSpeedAndTemperatureTrendChart(stats.averageSpeedTrend, stats.averageTemperatureTrend ?? [])
      : null;
  });

  /** Dual-axis scales (speed left, temperature right) for the trend chart. */
  readonly speedChartOptions = SPEED_TEMPERATURE_TREND_OPTIONS;

  /** Short month name for a 1-based month number (e.g. 7 → "Jul"), for the best-month tiles. */
  monthLabel(month: number): string {
    return MONTH_LABELS[month - 1] ?? '';
  }

  constructor() {
    this.dashboardService.getDashboard().subscribe((stats) => this.stats.set(stats));
  }
}
