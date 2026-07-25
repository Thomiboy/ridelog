import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import type { ChartData, ChartOptions, ChartType } from 'chart.js';
import { Statistics } from './statistics';
import { StatisticsService } from '../../core/api/statistics.service';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import type { StatisticsResult } from '../../core/api/statistics.models';
import type { LongestRideRoute } from '../../core/api/ride.models';
import { Chart } from '../../shared/chart/chart';
import { translocoTesting } from '../../core/i18n/transloco-testing';

// Chart.js needs a real canvas; stub the chart so the page renders in jsdom.
@Component({ selector: 'app-chart', template: '' })
class ChartStub {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartData>();
  readonly options = input<ChartOptions>();
}

describe('Statistics', () => {
  const stats: StatisticsResult = {
    monthlyAggregates: [
      { year: 2025, month: 7, distanceKm: 80, elevationGainMeters: 300, rideCount: 1, calories: 1000 },
      { year: 2026, month: 3, distanceKm: 100, elevationGainMeters: 500, rideCount: 1, calories: 1500 },
      { year: 2026, month: 7, distanceKm: 100, elevationGainMeters: 600, rideCount: 2, calories: 1300 },
    ],
    records: {
      longestRide: { id: 'ride-1', date: '2026-06-01T08:00:00+00:00', distanceKm: 120 },
      fastestAverage: { id: 'ride-2', date: '2026-06-02T08:00:00+00:00', averageSpeedKmh: 35 },
      longestStreak: { days: 3, startDate: '2026-06-01', endDate: '2026-06-03' },
    },
  };

  const longestRoutes: LongestRideRoute[] = [
    { id: 'ride-a', date: '2026-06-01T08:00:00+00:00', distanceKm: 120, routePolyline: 'poly-a' },
    { id: 'ride-b', date: '2026-05-01T08:00:00+00:00', distanceKm: 90, routePolyline: 'poly-b' },
    { id: 'ride-c', date: '2026-04-01T08:00:00+00:00', distanceKm: 60, routePolyline: 'poly-c' },
  ];

  function setup(override: Partial<StatisticsResult> = {}, routes: LongestRideRoute[] = longestRoutes) {
    const statisticsService = { getStatistics: vi.fn().mockReturnValue(of({ ...stats, ...override })) };
    const ridesService = { getLongestRides: vi.fn().mockReturnValue(of(routes)) };
    const mapState = { showRoutes: vi.fn(), reset: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Statistics, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: StatisticsService, useValue: statisticsService },
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
      ],
    }).overrideComponent(Statistics, {
      remove: { imports: [Chart] },
      add: { imports: [ChartStub] },
    });
    const fixture = TestBed.createComponent(Statistics);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState };
  }

  function chartData(fixture: ReturnType<typeof setup>['fixture'], name: string): ChartData {
    const node = fixture.debugElement.query(By.css(`[data-chart="${name}"] app-chart`));
    return (node.componentInstance as ChartStub).data();
  }

  it('shows the HR-zones section when zone data is present', () => {
    const { fixture, el } = setup({
      hrZones: [
        { zone: 1, minutes: 0 },
        { zone: 2, minutes: 30 },
        { zone: 3, minutes: 45 },
        { zone: 4, minutes: 12 },
        { zone: 5, minutes: 3 },
      ],
    });

    expect(el.querySelector('[data-section="hr-zones"]')).not.toBeNull();
    expect(chartData(fixture, 'hr-zones').datasets[0].data).toEqual([0, 30, 45, 12, 3]);
  });

  it('hides the HR-zones section when there is no zone data', () => {
    const { el } = setup({ hrZones: null });
    expect(el.querySelector('[data-section="hr-zones"]')).toBeNull();
  });

  it('shows the Temperature section with distribution, extremes and trend', () => {
    const { fixture, el } = setup({
      temperature: {
        distribution: [
          { fromCelsius: null, toCelsius: 0, km: 3 },
          { fromCelsius: 0, toCelsius: 5, km: 12 },
        ],
        coldest: { id: 'ride-cold', date: '2026-01-05T08:00:00+00:00', averageTemperatureCelsius: 2 },
        warmest: { id: 'ride-warm', date: '2026-07-05T08:00:00+00:00', averageTemperatureCelsius: 24 },
        seasonMinCelsius: -1,
        seasonMaxCelsius: 30,
        monthlyAverage: [
          { year: 2026, month: 1, averageTemperatureCelsius: 2 },
          { year: 2026, month: 7, averageTemperatureCelsius: 24 },
        ],
        yearlyDistribution: [
          { year: 2026, fromCelsius: null, toCelsius: 0, km: 3 },
          { year: 2026, fromCelsius: 0, toCelsius: 5, km: 12 },
        ],
      },
    });

    const section = el.querySelector('[data-section="temperature"]')!;
    expect(section).not.toBeNull();
    expect(chartData(fixture, 'temperature-distribution').datasets[0].data).toEqual([3, 12]);
    expect(chartData(fixture, 'temperature-trend').datasets[0].data).toEqual([2, 24]);
    // Warmest ride average is shown, linked to the ride.
    expect(section.textContent).toContain('24');
    expect(section.querySelector('a[href="/rides/ride-warm"]')).not.toBeNull();
  });

  it('hides the Temperature section without temperature data', () => {
    const { el } = setup({ temperature: null });
    expect(el.querySelector('[data-section="temperature"]')).toBeNull();
  });

  it('renders a year-filtered temperature distribution chart in Trends', () => {
    const { fixture } = setup({
      temperature: {
        distribution: [],
        coldest: null,
        warmest: null,
        seasonMinCelsius: null,
        seasonMaxCelsius: null,
        monthlyAverage: [],
        yearlyDistribution: [
          { year: 2025, fromCelsius: 0, toCelsius: 5, km: 99 },
          { year: 2026, fromCelsius: null, toCelsius: 0, km: 3 },
          { year: 2026, fromCelsius: 0, toCelsius: 5, km: 12 },
        ],
      },
    });

    // Active year defaults to 2026; only its bands appear (the 2025 band is filtered out).
    expect(chartData(fixture, 'temperature-by-year').datasets[0].data).toEqual([3, 12]);
  });

  it('renders the three records, linking rides where relevant', () => {
    const { el } = setup();

    const longest = el.querySelector('[data-record="longest-ride"]')!;
    expect(longest.textContent).toContain('120');
    expect(longest.querySelector('a')?.getAttribute('href')).toContain('/rides/ride-1');

    const fastest = el.querySelector('[data-record="fastest-average"]')!;
    expect(fastest.textContent).toContain('35');
    expect(fastest.querySelector('a')?.getAttribute('href')).toContain('/rides/ride-2');

    const streak = el.querySelector('[data-record="longest-streak"]')!;
    expect(streak.textContent).toContain('3');
  });

  it('renders the most-calories and longest-duration records, linking their rides', () => {
    const { el } = setup({
      records: {
        ...stats.records,
        mostCalories: { id: 'ride-cal', date: '2026-06-01T08:00:00+00:00', calories: 1500 },
        longestDuration: { id: 'ride-dur', date: '2026-06-02T08:00:00+00:00', durationMinutes: 185 },
      },
    });

    const calories = el.querySelector('[data-record="most-calories"]')!;
    expect(calories.textContent).toContain('1,500'); // grouped by the decimal pipe
    expect(calories.querySelector('a')?.getAttribute('href')).toContain('/rides/ride-cal');

    const duration = el.querySelector('[data-record="longest-duration"]')!;
    expect(duration.textContent).toContain('3h 5m'); // 185 minutes
    expect(duration.querySelector('a')?.getAttribute('href')).toContain('/rides/ride-dur');
  });

  it('defaults the year selector to the latest year with data', () => {
    const { el } = setup();

    const select = el.querySelector('[data-testid="year-select"]') as HTMLSelectElement;
    const options = [...select.options].map((o) => o.value);
    expect(options).toEqual(['2025', '2026']);
    expect(select.value).toBe('2026');
  });

  it('drives the monthly charts from the selected year and switches on change', () => {
    const { fixture, el } = setup();

    // Default 2026: July distance = 100.
    let distance = chartData(fixture, 'distance');
    expect(distance.datasets[0].label).toBe('2026');
    expect(distance.datasets[0].data[6]).toBe(100);

    // Switch to 2025: July distance = 80.
    const select = el.querySelector('[data-testid="year-select"]') as HTMLSelectElement;
    select.value = '2025';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    distance = chartData(fixture, 'distance');
    expect(distance.datasets[0].label).toBe('2025');
    expect(distance.datasets[0].data[6]).toBe(80);
  });

  it('renders all four monthly metric charts plus the year-over-year totals chart', () => {
    const { fixture } = setup();

    for (const name of ['distance', 'elevation', 'rides', 'calories', 'year-totals']) {
      expect(fixture.debugElement.query(By.css(`[data-chart="${name}"] app-chart`))).not.toBeNull();
    }
  });

  it('renders a rides-by-year chart summing each year in Trends', () => {
    const { fixture } = setup();

    // 2025 had 1 ride; 2026 had 1 + 2 = 3.
    expect(chartData(fixture, 'rides-by-year').datasets[0].data).toEqual([1, 3]);
  });

  it('paints the three longest routes on the background map when it opens', () => {
    const { ridesService, mapState } = setup();

    expect(ridesService.getLongestRides).toHaveBeenCalledWith(3);
    expect(mapState.showRoutes).toHaveBeenCalledWith(['poly-a', 'poly-b', 'poly-c']);
  });

  it('restores the default background map when it closes', () => {
    const { fixture, mapState } = setup();

    fixture.destroy();

    expect(mapState.reset).toHaveBeenCalled();
  });
});
