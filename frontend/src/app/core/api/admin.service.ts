import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { ImportSummary, PolarStatus, SyncSummary } from './admin.models';

/** Admin-only actions: link Polar, import files, trigger a sync. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getPolarStatus(): Observable<PolarStatus> {
    return this.http.get<PolarStatus>(`${this.baseUrl}/polar/status`);
  }

  getPolarAuthorizeUrl(): Observable<{ authorizeUrl: string }> {
    return this.http.get<{ authorizeUrl: string }>(`${this.baseUrl}/polar/authorize`);
  }

  sync(): Observable<SyncSummary> {
    return this.http.post<SyncSummary>(`${this.baseUrl}/sync`, null);
  }

  importRides(files: File[]): Observable<ImportSummary> {
    const form = new FormData();
    for (const file of files) {
      form.append('files', file, file.name);
    }
    return this.http.post<ImportSummary>(`${this.baseUrl}/import`, form);
  }
}
