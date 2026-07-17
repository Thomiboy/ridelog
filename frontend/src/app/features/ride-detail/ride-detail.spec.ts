import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { RideDetail } from './ride-detail';
import { RidesService } from '../../core/api/rides.service';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';
import { RouteMap } from './route-map/route-map';
import { translocoTesting } from '../../core/i18n/transloco-testing';

// Stub the map so this test never touches Leaflet (only route-map.spec mocks Leaflet, to avoid
// two conflicting module mocks in the bundled test environment).
@Component({ selector: 'app-route-map', template: '' })
class RouteMapStub {
  readonly polyline = input<string | null | undefined>();
}

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
    }).overrideComponent(RideDetail, {
      remove: { imports: [RouteMap] },
      add: { imports: [RouteMapStub] },
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

    const routeMap = fixture.debugElement.query(By.directive(RouteMapStub)).componentInstance as RouteMapStub;
    expect(routeMap.polyline()).toBe('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
  });
});
