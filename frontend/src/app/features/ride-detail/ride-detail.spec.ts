import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

// Leaflet needs a real DOM/canvas; mock it so the embedded map component instantiates in tests.
vi.mock('leaflet', () => {
  const line: Record<string, unknown> = { getBounds: vi.fn(() => 'BOUNDS'), remove: vi.fn() };
  line['addTo'] = vi.fn(() => line);
  return {
    map: vi.fn(() => ({ setView: vi.fn(), fitBounds: vi.fn(), remove: vi.fn() })),
    tileLayer: vi.fn(() => ({ addTo: vi.fn() })),
    polyline: vi.fn(() => line),
  };
});

import { RideDetail } from './ride-detail';
import { RidesService } from '../../core/api/rides.service';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';
import { RouteMap } from './route-map/route-map';
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
    routePolyline: '_p~iF~ps|U_ulLnnqC_mqNvxq`@',
  };

  function setup() {
    const ridesService = { getRide: vi.fn().mockReturnValue(of(detail)) };
    TestBed.configureTestingModule({
      imports: [RideDetail, translocoTesting()],
      providers: [
        { provide: RidesService, useValue: ridesService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'r1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(RideDetail);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService };
  }

  it('loads the ride by route id and shows its metrics', () => {
    const { el, ridesService } = setup();

    expect(ridesService.getRide).toHaveBeenCalledWith('r1');
    expect(el.textContent).toContain('61.5'); // distance
    expect(el.textContent).toContain('178'); // max HR
    expect(el.textContent).toContain('Polar'); // source badge
  });

  it('passes the route polyline to the map component', () => {
    const { fixture } = setup();

    const routeMap = fixture.debugElement.query(By.directive(RouteMap)).componentInstance as RouteMap;
    expect(routeMap.polyline()).toBe('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
  });
});
