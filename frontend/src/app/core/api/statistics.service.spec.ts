import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StatisticsService } from './statistics.service';
import { environment } from '../../../environments/environment';

describe('StatisticsService', () => {
  it('requests the statistics feed', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const service = TestBed.inject(StatisticsService);
    const http = TestBed.inject(HttpTestingController);

    let years: number | undefined;
    service.getStatistics().subscribe((s) => (years = s.monthlyAggregates.length));

    const request = http.expectOne(`${environment.apiBaseUrl}/statistics`);
    expect(request.request.method).toBe('GET');
    request.flush({
      monthlyAggregates: [{ year: 2026, month: 7, distanceKm: 100, elevationGainMeters: 600, rideCount: 2, calories: 1300 }],
      records: { longestRide: null, fastestAverage: null, longestStreak: null },
    });

    expect(years).toBe(1);
    http.verify();
  });
});
