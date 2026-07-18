import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { DashboardService } from '../../core/api/dashboard.service';
import type { DashboardStats } from '../../core/api/dashboard.models';
import { Chart } from '../../shared/chart/chart';
import { buildMonthlyDistanceChart, buildSpeedTrendChart } from './dashboard-charts';

@Component({
  selector: 'app-dashboard',
  imports: [Chart, TranslocoPipe, DecimalPipe],
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
    return stats ? buildSpeedTrendChart(stats.averageSpeedTrend) : null;
  });

  constructor() {
    this.dashboardService.getDashboard().subscribe((stats) => this.stats.set(stats));
  }
}
