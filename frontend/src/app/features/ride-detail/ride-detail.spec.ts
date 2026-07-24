import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { vi } from 'vitest';
import { RideDetail } from './ride-detail';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

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

  function setup(ride: RideDetailDto = detail, getRideImpl?: (id: string) => Observable<RideDetailDto>) {
    const ridesService = {
      getRide: getRideImpl ? vi.fn().mockImplementation(getRideImpl) : vi.fn().mockReturnValue(of(ride)),
    };
    const mapState = { showRoute: vi.fn() };
    const sheetState = { request: vi.fn() };
    const paramMap$ = new BehaviorSubject(convertToParamMap({ id: 'r1' }));
    TestBed.configureTestingModule({
      imports: [RideDetail, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
        { provide: SheetState, useValue: sheetState },
        { provide: ActivatedRoute, useValue: { paramMap: paramMap$.asObservable() } },
      ],
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
});
