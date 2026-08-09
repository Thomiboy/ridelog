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
  /** What this rider is using of the shared database — the figure the page exists to show. */
  rideCount: number;
  storageBytes: number;
  polarLinked: boolean;
  lastSyncAt: string | null;
  /** Whose rides a signed-out visitor is served. Exactly one rider carries this. */
  isPublicLog: boolean;
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

  /** Points the public log at a rider; refused (409) unless they are approved. */
  setPublicLog(riderId: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/riders/public-log`, { riderId });
  }

  /** Letting a rider in, or shutting them out — rejecting an approved rider is what a ban is. */
  setApproval(riderId: string, approval: Approval): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/riders/${riderId}/approval`, { approval });
  }
}
