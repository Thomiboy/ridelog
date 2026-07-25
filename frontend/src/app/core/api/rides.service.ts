import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { LongestRideRoute, Paged, RideDetail, RideRoute, RideSummary } from './ride.models';

/** Typed client for the public ride read endpoints. */
@Injectable({ providedIn: 'root' })
export class RidesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getRides(page = 1, pageSize = 20): Observable<Paged<RideSummary>> {
    return this.http.get<Paged<RideSummary>>(`${this.baseUrl}/rides?page=${page}&pageSize=${pageSize}`);
  }

  getRide(id: string): Observable<RideDetail> {
    return this.http.get<RideDetail>(`${this.baseUrl}/rides/${id}`);
  }

  /** The longest cycling routes for the background map, longest first (routes only). */
  getLongestRides(take = 3): Observable<LongestRideRoute[]> {
    return this.http.get<LongestRideRoute[]>(`${this.baseUrl}/rides/longest?take=${take}`);
  }

  /** Every cycling route (routes only) for the all-routes coverage map. */
  getAllRoutes(): Observable<RideRoute[]> {
    return this.http.get<RideRoute[]>(`${this.baseUrl}/rides/routes`);
  }

  deleteRide(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rides/${id}`);
  }
}
