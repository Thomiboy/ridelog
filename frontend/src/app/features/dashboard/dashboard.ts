import { TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatCardModule } from '@angular/material/card';
import { DashboardService } from '../../core/api/dashboard.service';
import type { DashboardStats } from '../../core/api/dashboard.models';
import { LanguageService } from '../../core/i18n/language.service';
import { monthLabels } from '../../core/i18n/month-labels';
import { Chart } from '../../shared/chart/chart';
import {
  buildMonthlyDistanceChart,
  buildSpeedAndTemperatureTrendChart,
  SPEED_TEMPERATURE_TREND_OPTIONS,
} from './dashboard-charts';

@Component({
  selector: 'app-dashboard',
  imports: [Chart, TranslocoPipe, TranslocoDecimalPipe, MatCardModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly dashboardService = inject(DashboardService);
  private readonly language = inject(LanguageService);

  /** Localized short month names; recomputes when the language changes. */
  private readonly months = computed(() => monthLabels(this.language.current()));

  readonly stats = signal<DashboardStats | null>(null);

  readonly distanceChart = computed(() => {
    const stats = this.stats();
    return stats ? buildMonthlyDistanceChart(stats.monthlyDistance, this.months()) : null;
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
    return this.months()[month - 1] ?? '';
  }

  constructor() {
    this.dashboardService.getDashboard().subscribe((stats) => this.stats.set(stats));
  }
}
