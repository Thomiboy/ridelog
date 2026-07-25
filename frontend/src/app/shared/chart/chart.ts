import { Component, effect, inject, input, viewChild } from '@angular/core';
import { BaseChartDirective } from 'ng2-charts';
import { Chart as ChartJs, type ChartData, type ChartOptions, type ChartType } from 'chart.js';
import { ThemeService } from '../../core/theme/theme.service';
import { applyChartTheme, chartThemeColors, type ThemeableChart } from './chart-theme';

/**
 * The only place Chart.js/ng2-charts is used directly, so the chart engine can later be swapped
 * (ECharts is on the backlog) by replacing this component.
 */
@Component({
  selector: 'app-chart',
  imports: [BaseChartDirective],
  template: `<canvas baseChart [type]="type()" [data]="data()" [options]="options()"></canvas>`,
  // Fill the fixed-height chart host. Without this the element stays inline (auto height), so
  // Chart.js falls back to its default canvas height and leaves empty space below the chart.
  styles: `:host { display: block; height: 100%; }`,
})
export class Chart {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartData>();
  readonly options = input<ChartOptions>({ responsive: true, maintainAspectRatio: false });

  private readonly theme = inject(ThemeService);
  private readonly chart = viewChild(BaseChartDirective);

  constructor() {
    // Axis text and gridlines follow the theme. Set the global defaults (so charts built later start
    // themed) but NOT `borderColor` — that would defeat the ng2-charts Colors plugin, which
    // auto-assigns a series colour to any dataset without an explicit one. Existing charts don't pick
    // up default changes on update(), so also retheme the live instance directly, then re-render.
    effect(() => {
      const colors = chartThemeColors(this.theme.resolved());
      ChartJs.defaults.color = colors.text;
      ChartJs.defaults.scale.grid.color = colors.grid;
      const chart = this.chart()?.chart;
      if (chart) {
        applyChartTheme(chart as unknown as ThemeableChart, colors);
        chart.update();
      }
    });
  }
}
