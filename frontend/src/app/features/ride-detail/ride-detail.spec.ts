import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
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
    source: 'Polar',
    averageSpeedKmh: 31.3,
    maximumSpeedKmh: 58.9,
    averageHeartRate: 142,
    maximumHeartRate: 178,
    elevationGainMeters: 460,
    averageCadence: 84,
    calories: 620,
    routePolyline: '_p~iF~ps|U_ulLnnqC_mqNvxq`@',
  };

  function setup() {
    const ridesService = { getRide: vi.fn().mockReturnValue(of(detail)) };
    const mapState = { showRoute: vi.fn() };
    const sheetState = { request: vi.fn() };
    TestBed.configureTestingModule({
      imports: [RideDetail, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
        { provide: SheetState, useValue: sheetState },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'r1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(RideDetail);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState, sheetState };
  }

  it('loads the ride by route id and shows its metrics', () => {
    const { el, ridesService } = setup();

    expect(ridesService.getRide).toHaveBeenCalledWith('r1');
    expect(el.textContent).toContain('61.5'); // distance
    expect(el.textContent).toContain('178'); // max HR
    expect(el.textContent).toContain('Polar'); // source badge
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
});
