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
    lastYearBestMonth: { month: 7, distanceKm: 80, rideCount: 1 },
    monthlyDistance: [
      { year: 2025, month: 7, distanceKm: 80 },
      { year: 2026, month: 7, distanceKm: 100 },
    ],
    averageSpeedTrend: [{ year: 2026, month: 7, averageSpeedKmh: 31 }],
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

  it('renders the previous-year tiles with the best month named', () => {
    const { el } = setup();

    expect(el.querySelector('[data-tile="last-year-distance"]')?.textContent).toContain('80');
    expect(el.querySelector('[data-tile="last-year-rides"]')?.textContent).toContain('1');
    expect(el.querySelector('[data-tile="last-year-elevation"]')?.textContent).toContain('300');
    // The best-month tiles name the month (July → "Jul").
    expect(el.querySelector('[data-tile="best-month-distance"]')?.textContent).toContain('Jul');
    expect(el.querySelector('[data-tile="best-month-distance"]')?.textContent).toContain('80');
    expect(el.querySelector('[data-tile="best-month-rides"]')?.textContent).toContain('Jul');
  });

  it('hides the previous-year tiles when there were no rides last year', () => {
    const { el } = setup({ lastYear: { distanceKm: 0, rideCount: 0, elevationGainMeters: 0 }, lastYearBestMonth: null });

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
    expect(line.data().datasets[0].data).toEqual([31]);
  });
});
