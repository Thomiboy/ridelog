import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import type { ChartData, ChartOptions, ChartType } from 'chart.js';
import { Dashboard } from './dashboard';
import { DashboardService } from '../../core/api/dashboard.service';
import type { DashboardStats } from '../../core/api/dashboard.models';
import { Chart } from '../../shared/chart/chart';
import { translocoTesting } from '../../core/i18n/transloco-testing';

// Chart.js needs a real canvas; stub the chart so the dashboard renders in jsdom.
@Component({ selector: 'app-chart', template: '' })
class ChartStub {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartData>();
  readonly options = input<ChartOptions>();
}

describe('Dashboard', () => {
  const stats: DashboardStats = {
    thisMonth: { distanceKm: 100, rideCount: 2, elevationGainMeters: 600 },
    thisYear: { distanceKm: 200.5, rideCount: 3, elevationGainMeters: 1100 },
    lastYear: { distanceKm: 80, rideCount: 1, elevationGainMeters: 300 },
    sameMonthLastYear: { year: 2025, month: 7, distanceKm: 80, rideCount: 1 },
    monthlyDistance: [
      { year: 2025, month: 7, distanceKm: 80 },
      { year: 2026, month: 7, distanceKm: 100 },
    ],
    averageSpeedTrend: [{ year: 2026, month: 7, averageSpeedKmh: 31 }],
    averageTemperatureTrend: [{ year: 2026, month: 7, averageTemperatureCelsius: 22 }],
  };

  function setup(override: Partial<DashboardStats> = {}) {
    const dashboardService = { getDashboard: vi.fn().mockReturnValue(of({ ...stats, ...override })) };
    TestBed.configureTestingModule({
      imports: [Dashboard, translocoTesting()],
      providers: [{ provide: DashboardService, useValue: dashboardService }],
    }).overrideComponent(Dashboard, {
      remove: { imports: [Chart] },
      add: { imports: [ChartStub] },
    });
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('renders the stat tiles with the aggregates', () => {
    const { el } = setup();

    expect(el.querySelector('[data-tile="month-distance"]')?.textContent).toContain('100');
    expect(el.querySelector('[data-tile="year-distance"]')?.textContent).toContain('200.5');
    expect(el.querySelector('[data-tile="year-rides"]')?.textContent).toContain('3');
    expect(el.querySelector('[data-tile="year-elevation"]')?.textContent).toContain('1,100');
  });

  it('renders the previous-year totals', () => {
    const { el } = setup();

    expect(el.querySelector('[data-tile="last-year-distance"]')?.textContent).toContain('80');
    expect(el.querySelector('[data-tile="last-year-rides"]')?.textContent).toContain('1');
    expect(el.querySelector('[data-tile="last-year-elevation"]')?.textContent).toContain('300');
  });

  it('renders the same month last year, naming that month and its year', () => {
    const { el } = setup();

    const distance = el.querySelector('[data-tile="same-month-last-year-distance"]');
    expect(distance?.textContent).toContain('80');
    // Naming the year makes clear this is a whole past month, not the part-elapsed current one.
    expect(distance?.textContent).toContain('July 2025');

    const rides = el.querySelector('[data-tile="same-month-last-year-rides"]');
    expect(rides?.textContent).toContain('1');
    expect(rides?.textContent).toContain('July 2025');
  });

  it('hides the previous-year tiles when there were no rides last year', () => {
    const { el } = setup({ lastYear: { distanceKm: 0, rideCount: 0, elevationGainMeters: 0 } });

    expect(el.querySelector('[data-tile="last-year-distance"]')).toBeNull();
    expect(el.querySelector('[data-tile="same-month-last-year-distance"]')).toBeNull();
  });

  it('renders without crashing when the API omits last-year data (older backend)', () => {
    const { el } = setup({ lastYear: undefined, sameMonthLastYear: undefined });

    // The current-period tiles still render; the previous-year group is simply absent.
    expect(el.querySelector('[data-tile="year-distance"]')?.textContent).toContain('200.5');
    expect(el.querySelector('[data-tile="last-year-distance"]')).toBeNull();
    expect(el.querySelector('[data-tile="best-month-distance"]')).toBeNull();
  });

  it('feeds both charts from the aggregates', () => {
    const { fixture } = setup();

    const charts = fixture.debugElement.children
      .flatMap((child) => child.queryAll(() => true))
      .filter((node) => node.componentInstance instanceof ChartStub)
      .map((node) => node.componentInstance as ChartStub);

    expect(charts.length).toBe(2);
    const bar = charts.find((c) => c.type() === 'bar')!;
    const line = charts.find((c) => c.type() === 'line')!;
    expect(bar.data().datasets.length).toBe(2); // current + previous year

    // The trend line carries speed and temperature on separate y-axes.
    const datasets = line.data().datasets as Array<{ yAxisID?: string; data: unknown }>;
    const speed = datasets.find((d) => d.yAxisID === 'speed')!;
    const temperature = datasets.find((d) => d.yAxisID === 'temp')!;
    expect(speed.data).toEqual([31]);
    expect(temperature.data).toEqual([22]);
    expect(line.options()?.scales).toMatchObject({ speed: { position: 'left' }, temp: { position: 'right' } });
  });
});
