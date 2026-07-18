import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MapState } from './map-state';
import { environment } from '../../../environments/environment';

describe('MapState', () => {
  let state: MapState;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    state = TestBed.inject(MapState);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the latest ride route as the default background', () => {
    state.loadLatest();

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [{ id: 'r9', startTime: '2026-07-17T08:00:00Z', distanceKm: 42, durationMinutes: 90, sport: 'ROAD_BIKING' }],
      page: 1,
      pageSize: 1,
      total: 5,
    });
    http.expectOne(`${environment.apiBaseUrl}/rides/r9`).flush({
      id: 'r9',
      routePolyline: '_p~iF~ps|U_ulLnnqC_mqNvxq`@',
    });

    expect(state.polyline()).toBe('_p~iF~ps|U_ulLnnqC_mqNvxq`@');
  });

  it('stays empty when there are no rides yet', () => {
    state.loadLatest();

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [],
      page: 1,
      pageSize: 1,
      total: 0,
    });

    expect(state.polyline()).toBeNull();
  });

  it('showRoute overrides the background route', () => {
    state.showRoute('abc123');

    expect(state.polyline()).toBe('abc123');
  });

  it('reset restores the latest route from cache without refetching', () => {
    state.loadLatest();
    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [{ id: 'r9', startTime: '2026-07-17T08:00:00Z', distanceKm: 42, durationMinutes: 90, sport: 'ROAD_BIKING' }],
      page: 1,
      pageSize: 1,
      total: 5,
    });
    http.expectOne(`${environment.apiBaseUrl}/rides/r9`).flush({ id: 'r9', routePolyline: 'latest-route' });

    state.showRoute('selected-route');
    expect(state.polyline()).toBe('selected-route');

    state.reset();

    expect(state.polyline()).toBe('latest-route');
    http.verify(); // no new requests
  });

  it('reset loads the latest route when nothing is cached yet', () => {
    state.reset();

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [],
      page: 1,
      pageSize: 1,
      total: 0,
    });

    expect(state.polyline()).toBeNull();
  });
});
