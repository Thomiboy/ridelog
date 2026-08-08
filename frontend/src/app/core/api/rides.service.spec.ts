import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RidesService } from './rides.service';
import type { Paged, RideSummary } from './ride.models';
import { environment } from '../../../environments/environment';

describe('RidesService', () => {
  let service: RidesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RidesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests a page of rides from the API', () => {
    const paged: Paged<RideSummary> = {
      items: [
        {
          id: 'r1',
          startTime: '2026-06-01T08:00:00Z',
          distanceKm: 61.5,
          durationMinutes: 118,
          sport: 'ROAD_BIKING',
          sources: ['PolarAutoSync'],
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    };

    let received: Paged<RideSummary> | undefined;
    service.getRides(1, 20).subscribe((r) => (received = r));

    const request = http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=20`);
    expect(request.request.method).toBe('GET');
    request.flush(paged);

    expect(received).toEqual(paged);
  });

  it('requests a single ride by id', () => {
    service.getRide('r1').subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/rides/r1`);
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('deletes a ride by id', () => {
    service.deleteRide('r1').subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/rides/r1`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  const summary = (id: string): RideSummary => ({
    id,
    startTime: '2026-07-05T08:00:00Z',
    distanceKm: 40,
    durationMinutes: 60,
    sport: 'ROAD_BIKING',
    sources: [],
  });

  it('getAllRides returns every ride in one request when they fit on a page', () => {
    let result: RideSummary[] | undefined;
    service.getAllRides().subscribe((rides) => (result = rides));

    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=100`).flush({
      items: [summary('r1'), summary('r2')],
      page: 1,
      pageSize: 100,
      total: 2,
    });

    expect(result?.map((r) => r.id)).toEqual(['r1', 'r2']);
  });

  it('getAllRides pages through every ride when there is more than one page', () => {
    let result: RideSummary[] | undefined;
    service.getAllRides().subscribe((rides) => (result = rides));

    // total 150 at 100 per page → two pages.
    http.expectOne(`${environment.apiBaseUrl}/rides?page=1&pageSize=100`).flush({
      items: [summary('r1')],
      page: 1,
      pageSize: 100,
      total: 150,
    });
    http.expectOne(`${environment.apiBaseUrl}/rides?page=2&pageSize=100`).flush({
      items: [summary('r2')],
      page: 2,
      pageSize: 100,
      total: 150,
    });

    expect(result?.map((r) => r.id)).toEqual(['r1', 'r2']);
  });

  // Other activities are a sibling of rides, not a filter over them, so they come from their own
  // endpoint — the rides call is left exactly as it was.
  it('asks a separate endpoint for the activities that are not rides', () => {
    let page: { items: { sport: string }[] } | undefined;
    service.getOtherActivities().subscribe((p) => (page = p));

    const request = http.expectOne(`${environment.apiBaseUrl}/activities?page=1&pageSize=20`);
    expect(request.request.method).toBe('GET');
    request.flush({ items: [{ id: 'a1', sport: 'RUNNING' }], page: 1, pageSize: 20, total: 1 });

    expect(page!.items[0].sport).toBe('RUNNING');
  });
});
