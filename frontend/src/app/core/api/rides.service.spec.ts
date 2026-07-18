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
        { id: 'r1', startTime: '2026-06-01T08:00:00Z', distanceKm: 61.5, durationMinutes: 118, sport: 'ROAD_BIKING' },
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
});
