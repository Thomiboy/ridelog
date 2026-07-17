import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { RideDetail, RideSummary } from './ride.models';

/** Typed client for the public ride read endpoints. */
@Injectable({ providedIn: 'root' })
export class RidesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getRides(): Observable<RideSummary[]> {
    return this.http.get<RideSummary[]>(`${this.baseUrl}/rides`);
  }

  getRide(id: string): Observable<RideDetail> {
    return this.http.get<RideDetail>(`${this.baseUrl}/rides/${id}`);
  }
}
