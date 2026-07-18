import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { Paged, RideDetail, RideSummary } from './ride.models';

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

  deleteRide(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rides/${id}`);
  }
}
