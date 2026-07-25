import { Component, input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { vi } from 'vitest';
import type { ChartData, ChartOptions, ChartType } from 'chart.js';
import { RideDetail } from './ride-detail';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { Chart } from '../../shared/chart/chart';
import type { RideDetail as RideDetailDto, RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

// Chart.js needs a real canvas; stub the chart so the detail renders in jsdom.
@Component({ selector: 'app-chart', template: '' })
class ChartStub {
  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartData>();
  readonly options = input<ChartOptions>();
}

describe('RideDetail', () => {
  const detail: RideDetailDto = {
    id: 'r1',
    startTime: '2026-06-01T08:00:00Z',
    endTime: '2026-06-01T10:00:00Z',
    distanceKm: 61.5,
    durationMinutes: 118,
    sport: 'ROAD_BIKING',
    sources: ['PolarAutoSync'],
    averageSpeedKmh: 31.3,
    maximumSpeedKmh: 58.9,
    averageHeartRate: 142,
    maximumHeartRate: 178,
    elevationGainMeters: 460,
    averageCadence: 84,
    calories: 620,
    previousId: 'r0',
    nextId: 'r2',
    routePolyline: '_p~iF~ps|U_ulLnnqC_mqNvxq`@',
  };

  const summary = (id: string, distanceKm: number): RideSummary => ({
    id,
    startTime: '2026-05-01T08:00:00Z',
    distanceKm,
    durationMinutes: 90,
    sport: 'ROAD_BIKING',
    sources: [],
  });

  function setup(
    ride: RideDetailDto = detail,
    getRideImpl?: (id: string) => Observable<RideDetailDto>,
    admin = false,
    allRides: RideSummary[] = [summary('r1', 61.5), summary('r2', 50)],
  ) {
    const ridesService = {
      getRide: getRideImpl ? vi.fn().mockImplementation(getRideImpl) : vi.fn().mockReturnValue(of(ride)),
      reprocessRide: vi.fn().mockReturnValue(of(void 0)),
      getAllRides: vi.fn().mockReturnValue(of(allRides)),
    };
    const mapState = { showRoute: vi.fn(), showRoutes: vi.fn() };
    const sheetState = { request: vi.fn() };
    const authService = { isAdmin: signal(admin) };
    const paramMap$ = new BehaviorSubject(convertToParamMap({ id: 'r1' }));
    TestBed.configureTestingModule({
      imports: [RideDetail, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
        { provide: SheetState, useValue: sheetState },
        { provide: AuthService, useValue: authService },
        { provide: ActivatedRoute, useValue: { paramMap: paramMap$.asObservable() } },
      ],
    }).overrideComponent(RideDetail, {
      remove: { imports: [Chart] },
      add: { imports: [ChartStub] },
    });
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(RideDetail);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState, sheetState, router, paramMap$ };
  }

  it('loads the ride by route id and shows its metrics', () => {
    const { el, ridesService } = setup();

    expect(ridesService.getRide).toHaveBeenCalledWith('r1');
    expect(el.textContent).toContain('61.5'); // distance
    expect(el.textContent).toContain('178'); // max HR
  });

  const withSeries = (): RideDetailDto => ({
    ...detail,
    metricSeries: [
      { distanceKm: 0, elapsedMinutes: 0, elevationMeters: 100, heartRate: 120 },
      { distanceKm: 2, elapsedMinutes: 10, elevationMeters: 140, heartRate: 150 },
    ],
  });

  function graphChart(fixture: ReturnType<typeof setup>['fixture']): ChartStub | null {
    const node = fixture.debugElement.query(By.css('[data-graph] app-chart'));
    return node ? (node.componentInstance as ChartStub) : null;
  }

  it('shows the elevation/HR graph, x-axis by distance, when a series is present', () => {
    const { fixture } = setup(withSeries());

    const chart = graphChart(fixture);
    expect(chart).not.toBeNull();
    expect(chart!.data().labels).toEqual([0, 2]); // distance km
  });

  it('switches the graph x-axis from distance to elapsed time', () => {
    const { fixture, el } = setup(withSeries());

    (el.querySelector('[data-axis="time"]') as HTMLElement).click();
    fixture.detectChanges();

    expect(graphChart(fixture)!.data().labels).toEqual([0, 10]); // elapsed minutes
  });

  it('shows the HR-zone chart when zones have time', () => {
    const { fixture, el } = setup({
      ...detail,
      hrZones: [
        { zone: 1, minutes: 0 },
        { zone: 2, minutes: 10 },
        { zone: 3, minutes: 25 },
        { zone: 4, minutes: 0 },
        { zone: 5, minutes: 0 },
      ],
    });

    expect(el.querySelector('[data-hr-zones]')).not.toBeNull();
    const node = fixture.debugElement.query(By.css('[data-hr-zones] app-chart'));
    expect((node.componentInstance as ChartStub).data().datasets[0].data).toEqual([0, 10, 25, 0, 0]);
  });

  it('hides the HR-zone chart when there are no zones', () => {
    const { el } = setup({ ...detail, hrZones: null });
    expect(el.querySelector('[data-hr-zones]')).toBeNull();
  });

  it('hides the HR-zone chart when every zone is empty', () => {
    const { el } = setup({
      ...detail,
      hrZones: [
        { zone: 1, minutes: 0 },
        { zone: 2, minutes: 0 },
        { zone: 3, minutes: 0 },
        { zone: 4, minutes: 0 },
        { zone: 5, minutes: 0 },
      ],
    });
    expect(el.querySelector('[data-hr-zones]')).toBeNull();
  });

  it('hides the graph when the ride has no series', () => {
    const { el } = setup({ ...detail, metricSeries: null });

    expect(el.querySelector('[data-graph]')).toBeNull();
  });

  it('hides the graph when the series has neither elevation nor heart rate', () => {
    const { el } = setup({
      ...detail,
      metricSeries: [
        { distanceKm: 0, elapsedMinutes: 0, elevationMeters: null, heartRate: null },
        { distanceKm: 1, elapsedMinutes: 5, elevationMeters: null, heartRate: null },
      ],
    });

    expect(el.querySelector('[data-graph]')).toBeNull();
  });

  it('shows a temperature card with average, min and max when recorded', () => {
    const { el } = setup({
      ...detail,
      averageTemperatureCelsius: 12.5,
      minTemperatureCelsius: 8,
      maxTemperatureCelsius: 18,
    });

    const card = el.querySelector('[data-card="temperature"]')!;
    expect(card).not.toBeNull();
    expect(card.textContent).toContain('12.5');
    expect(card.textContent).toContain('8');
    expect(card.textContent).toContain('18');
  });

  it('hides the temperature card when no temperature was recorded', () => {
    const { el } = setup({ ...detail, averageTemperatureCelsius: null });
    expect(el.querySelector('[data-card="temperature"]')).toBeNull();
  });

  it('shows the source chips in the header', () => {
    const { el } = setup({ ...detail, sources: ['PolarAutoSync', 'Bryton'] });

    const chips = [...el.querySelectorAll('.detail-header [data-source-chip]')].map((c) => c.textContent?.trim());
    expect(chips).toEqual(['Polar · Auto-sync', 'Bryton']);
  });

  it('shows the start time alongside the date', () => {
    const { el } = setup();

    // A HH:mm time is rendered next to the date (timezone-independent check).
    expect(el.querySelector('.detail-header')?.textContent).toMatch(/\d{1,2}:\d{2}/);
  });

  it('shows the duration as hours and minutes', () => {
    const { el } = setup();

    expect(el.textContent).toContain('1h 58m'); // 118 minutes
    expect(el.textContent).not.toContain('118 min');
  });

  it('publishes the route to the global background map', () => {
    const { mapState } = setup();

    expect(mapState.showRoute).toHaveBeenCalledWith('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
  });

  it('shows calories and no longer shows cadence', () => {
    const { el } = setup();

    expect(el.textContent).toContain('620'); // calories value
    expect(el.textContent).toContain('Calories');
    expect(el.textContent).not.toContain('Cadence');
  });

  it('snaps the sheet to half so the route is visible', () => {
    const { sheetState } = setup();

    expect(sheetState.request).toHaveBeenCalledWith('half');
  });

  it('offers a way back to the ride list', () => {
    const { el } = setup();

    expect(el.querySelector('a[href="/rides"]')).toBeTruthy();
  });

  it('steps to the previous (older) and next (newer) ride', () => {
    const { el, router } = setup();
    const navigate = vi.spyOn(router, 'navigateByUrl');

    (el.querySelector('[data-prev-ride]') as HTMLButtonElement).click();
    expect(navigate).toHaveBeenCalledWith('/rides/r0');

    (el.querySelector('[data-next-ride]') as HTMLButtonElement).click();
    expect(navigate).toHaveBeenCalledWith('/rides/r2');
  });

  it('reloads the ride and route when the route id changes', () => {
    const { ridesService, mapState, paramMap$ } = setup(detail, (id) =>
      of({ ...detail, id, routePolyline: `route-${id}` }),
    );

    paramMap$.next(convertToParamMap({ id: 'r2' }));

    expect(ridesService.getRide).toHaveBeenCalledWith('r2');
    expect(mapState.showRoute).toHaveBeenCalledWith('route-r2');
  });

  it('groups the metrics into four icon-led cards', () => {
    const { el } = setup();

    expect(el.querySelectorAll('[data-card]').length).toBe(4);
    // Per-metric Material Icon ligatures are wired (e.g. the calories fire icon).
    expect(el.textContent).toContain('local_fire_department');
    expect(el.textContent).toContain('favorite');
  });

  it('shows a dash for missing metrics', () => {
    const { el } = setup({ ...detail, maximumSpeedKmh: undefined, averageHeartRate: undefined });

    expect(el.textContent).toContain('—');
  });

  it('disables the stepper buttons at the ends of the list', () => {
    const { el } = setup({ ...detail, previousId: null, nextId: null });

    expect((el.querySelector('[data-prev-ride]') as HTMLButtonElement).disabled).toBe(true);
    expect((el.querySelector('[data-next-ride]') as HTMLButtonElement).disabled).toBe(true);
  });

  const compareDetail: RideDetailDto = {
    ...detail,
    id: 'r2',
    distanceKm: 50,
    routePolyline: 'compare-poly',
  };

  it('opens the compare picker over all rides', () => {
    const { el, fixture, ridesService } = setup();

    (el.querySelector('[data-compare]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(ridesService.getAllRides).toHaveBeenCalled();
    expect(el.querySelector('app-ride-picker')).not.toBeNull();
  });

  it('compares against the picked ride, showing deltas and overlaying both routes', () => {
    const { el, fixture, mapState } = setup(detail, (id) => of(id === 'r2' ? compareDetail : detail));

    (el.querySelector('[data-compare]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el.querySelector('[data-picker-row]') as HTMLButtonElement).click(); // the one non-current ride (r2)
    fixture.detectChanges();

    expect(el.querySelector('[data-compare-table]')).not.toBeNull();
    expect(mapState.showRoutes).toHaveBeenCalledWith([detail.routePolyline, 'compare-poly']);
  });

  it('exits comparison back to the single ride', () => {
    const { el, fixture, mapState } = setup(detail, (id) => of(id === 'r2' ? compareDetail : detail));

    (el.querySelector('[data-compare]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el.querySelector('[data-picker-row]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el.querySelector('[data-compare-exit]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('[data-compare-table]')).toBeNull();
    expect(mapState.showRoute).toHaveBeenLastCalledWith(detail.routePolyline);
  });

  it('hides the reprocess button from non-admins', () => {
    const { el } = setup();

    expect(el.querySelector('[data-reprocess]')).toBeNull();
  });

  it('reprocesses the ride and reloads it when an admin clicks reprocess', () => {
    const { el, ridesService } = setup(detail, undefined, true);
    ridesService.getRide.mockClear(); // ignore the initial load

    (el.querySelector('[data-reprocess]') as HTMLButtonElement).click();

    expect(ridesService.reprocessRide).toHaveBeenCalledWith('r1');
    expect(ridesService.getRide).toHaveBeenCalledWith('r1'); // reloaded to show refreshed metrics
  });
});
