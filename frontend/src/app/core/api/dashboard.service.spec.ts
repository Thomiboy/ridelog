import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { environment } from '../../../environments/environment';

describe('DashboardService', () => {
  it('requests the dashboard aggregates', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const service = TestBed.inject(DashboardService);
    const http = TestBed.inject(HttpTestingController);

    let distance: number | undefined;
    service.getDashboard().subscribe((d) => (distance = d.thisMonth.distanceKm));

    const request = http.expectOne(`${environment.apiBaseUrl}/dashboard`);
    expect(request.request.method).toBe('GET');
    request.flush({
      thisMonth: { distanceKm: 100, rideCount: 2, elevationGainMeters: 600 },
      thisYear: { distanceKm: 200, rideCount: 3, elevationGainMeters: 1100 },
      monthlyDistance: [],
      averageSpeedTrend: [],
    });

    expect(distance).toBe(100);
    http.verify();
  });
});
