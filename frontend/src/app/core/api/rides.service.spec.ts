import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RidesService } from './rides.service';
import type { RideSummary } from './ride.models';
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

  it('requests the ride list from the API', () => {
    const rides: RideSummary[] = [
      { id: 'r1', startTime: '2026-06-01T08:00:00Z', distanceKm: 61.5, durationMinutes: 118, sport: 'ROAD_BIKING' },
    ];

    let received: RideSummary[] | undefined;
    service.getRides().subscribe((r) => (received = r));

    const request = http.expectOne(`${environment.apiBaseUrl}/rides`);
    expect(request.request.method).toBe('GET');
    request.flush(rides);

    expect(received).toEqual(rides);
  });

  it('requests a single ride by id', () => {
    service.getRide('r1').subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/rides/r1`);
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
