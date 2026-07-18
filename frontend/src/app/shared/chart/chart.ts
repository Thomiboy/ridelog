import { Component, input } from '@angular/core';
import { BaseChartDirective } from 'ng2-charts';
import type { ChartData, ChartOptions, ChartType } from 'chart.js';

/**
 * The only place Chart.js/ng2-charts is used directly, so the chart engine can later be swapped
 * (ECharts is on the backlog) by replacing this component.
 */
@Component({
  selector: 'app-chart',
  imports: [BaseChartDirective],
  template: `<canvas baseChart [type]="type()" [data]="data()" [options]="options()"></canvas>`,
})
export class Chart {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartData>();
  readonly options = input<ChartOptions>({ responsive: true, maintainAspectRatio: false });
}
