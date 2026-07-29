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

    expect(state.routes()).toEqual(['_p~iF~ps|U_ulLnnqC_mqNvxq`@']);
  });

  it('stays empty when there are no rides yet', () => {
    state.loadLatest();

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [],
      page: 1,
      pageSize: 1,
      total: 0,
    });

    expect(state.routes()).toEqual([]);
  });

  it('showRoute overrides the background with a single route', () => {
    state.showRoute('abc123');

    expect(state.routes()).toEqual(['abc123']);
  });

  it('showRoute with nothing clears the background', () => {
    state.showRoute('abc123');
    state.showRoute(null);

    expect(state.routes()).toEqual([]);
  });

  it('showRoutes overrides the background with several routes', () => {
    state.showRoutes(['a', 'b', 'c']);

    expect(state.routes()).toEqual(['a', 'b', 'c']);
  });

  it('showRoute carries rest stops for the single route', () => {
    state.showRoute('abc', [{ latitude: 1, longitude: 2 }]);

    expect(state.restStops()).toEqual([{ latitude: 1, longitude: 2 }]);
  });

  it('showRoutes clears rest stops (they only make sense for a single route)', () => {
    state.showRoute('abc', [{ latitude: 1, longitude: 2 }]);
    state.showRoutes(['a', 'b']);

    expect(state.restStops()).toEqual([]);
  });

  it('showRoutes drops empty entries so the map never draws a blank track', () => {
    state.showRoutes(['a', '', 'c']);

    expect(state.routes()).toEqual(['a', 'c']);
  });

  it('showCoverage marks the routes as one coverage layer', () => {
    state.showCoverage(['a', 'b']);

    expect(state.routes()).toEqual(['a', 'b']);
    expect(state.coverage()).toBe(true);
  });

  it('showRoute and showRoutes draw distinct tracks, never coverage', () => {
    state.showCoverage(['a', 'b']);
    state.showRoutes(['x', 'y']);
    expect(state.coverage()).toBe(false);

    state.showCoverage(['a', 'b']);
    state.showRoute('single');
    expect(state.coverage()).toBe(false);
  });

  it('showAllRoutes paints every route as coverage, fetching them only once', () => {
    state.showAllRoutes();
    http.expectOne(`${environment.apiBaseUrl}/rides/routes`).flush([
      { id: 'r1', routePolyline: 'poly-1' },
      { id: 'r2', routePolyline: 'poly-2' },
    ]);

    expect(state.routes()).toEqual(['poly-1', 'poly-2']);
    expect(state.coverage()).toBe(true);

    // Leaving and returning to Rides must reuse the cache — this is the largest payload in the app.
    state.showRoute('a-single-ride');
    state.showAllRoutes();

    expect(state.routes()).toEqual(['poly-1', 'poly-2']);
    expect(state.coverage()).toBe(true);
    http.verify(); // no second request
  });

  it('invalidate makes showAllRoutes refetch, so imported rides appear', () => {
    state.showAllRoutes();
    http.expectOne(`${environment.apiBaseUrl}/rides/routes`).flush([{ id: 'r1', routePolyline: 'poly-1' }]);
    expect(state.routes()).toEqual(['poly-1']);

    state.invalidate();
    state.showAllRoutes();

    http.expectOne(`${environment.apiBaseUrl}/rides/routes`).flush([
      { id: 'r1', routePolyline: 'poly-1' },
      { id: 'r2', routePolyline: 'poly-2' },
    ]);
    expect(state.routes()).toEqual(['poly-1', 'poly-2']);
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

    state.showRoutes(['selected-a', 'selected-b']);
    expect(state.routes()).toEqual(['selected-a', 'selected-b']);

    state.reset();

    expect(state.routes()).toEqual(['latest-route']);
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

    expect(state.routes()).toEqual([]);
  });

  it('invalidate forces reset to refetch the latest route', () => {
    state.loadLatest();
    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [{ id: 'r9', startTime: '2026-07-17T08:00:00Z', distanceKm: 42, durationMinutes: 90, sport: 'ROAD_BIKING' }],
      page: 1,
      pageSize: 1,
      total: 5,
    });
    http.expectOne(`${environment.apiBaseUrl}/rides/r9`).flush({ id: 'r9', routePolyline: 'old-latest' });

    // The cached latest ride was deleted, so the cache must not be trusted any more.
    state.invalidate();
    state.reset();

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=1`).flush({
      items: [{ id: 'r8', startTime: '2026-07-16T08:00:00Z', distanceKm: 30, durationMinutes: 70, sport: 'ROAD_BIKING' }],
      page: 1,
      pageSize: 1,
      total: 4,
    });
    http.expectOne(`${environment.apiBaseUrl}/rides/r8`).flush({ id: 'r8', routePolyline: 'new-latest' });

    expect(state.routes()).toEqual(['new-latest']);
  });
});
