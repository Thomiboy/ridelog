import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Where a rider stands with the owner. Sent by name, as every enum on this wire is. */
export type Approval = 'Pending' | 'Approved' | 'Rejected';

export interface RiderSummary {
  id: string;
  email: string;
  approval: Approval;
}

/**
 * The owner's side of the door. The one API surface in this app that reaches across riders, which
 * is what the admin role is for.
 */
@Injectable({ providedIn: 'root' })
export class RidersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(): Observable<RiderSummary[]> {
    return this.http.get<RiderSummary[]>(`${this.baseUrl}/riders`);
  }

  /** Letting a rider in, or shutting them out — rejecting an approved rider is what a ban is. */
  setApproval(riderId: string, approval: Approval): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/riders/${riderId}/approval`, { approval });
  }
}
