import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { StatisticsResult } from './statistics.models';

/** Typed client for the public statistics aggregates and records. */
@Injectable({ providedIn: 'root' })
export class StatisticsService {
  private readonly http = inject(HttpClient);

  getStatistics(): Observable<StatisticsResult> {
    return this.http.get<StatisticsResult>(`${environment.apiBaseUrl}/statistics`);
  }
}
