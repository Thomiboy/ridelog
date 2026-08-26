import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { AnalysisLanguage, MonthlyAnalysisResult } from './analysis.models';

/**
 * The monthly analysis (#187). A rider's own month, so every call here needs their token — unlike
 * the rest of the statistics feed, none of this is public.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisService {
  private readonly http = inject(HttpClient);

  private get base(): string {
    return `${environment.apiBaseUrl}/statistics/analysis`;
  }

  /** The stored reading, or a 404 when the month has none. */
  read(year: number, month: number, language: AnalysisLanguage): Observable<MonthlyAnalysisResult> {
    return this.http.get<MonthlyAnalysisResult>(`${this.base}?year=${year}&month=${month}&language=${language}`);
  }

  /** Writes one. The only call here that costs anything, and it only ever happens on a press. */
  write(year: number, month: number, language: AnalysisLanguage): Observable<MonthlyAnalysisResult> {
    return this.http.post<MonthlyAnalysisResult>(this.base, { year, month, language });
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  /**
   * The owner's kill switch. Admin-only, and stored, so it takes effect without a restart (#172).
   * Off is the default nobody has to remember: this is the one feature that spends money per use.
   */
  setEnabled(enabled: boolean): Observable<void> {
    return this.http.put<void>(`${this.base}/switch`, { enabled });
  }
}
