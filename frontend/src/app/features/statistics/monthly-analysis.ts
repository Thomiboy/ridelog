import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AnalysisService } from '../../core/api/analysis.service';
import { LanguageService } from '../../core/i18n/language.service';
import type { AnalysisLanguage, AnalysisRefusal, MonthlyAnalysisResult } from '../../core/api/analysis.models';
import type { MonthlyAggregate } from '../../core/api/statistics.models';

/**
 * A written reading of one calendar month (#187).
 *
 * Two rules shape this component. A reading is only ever written on a press — nothing here generates
 * as a side effect of navigating, selecting a month, or switching language, because every generation
 * costs money. And generated prose cannot be translated: a Hungarian reading stays visible under an
 * English UI, with a note saying which language it was written in and an offer to write the other,
 * rather than quietly spending a second generation on the same month.
 */
@Component({
  selector: 'app-monthly-analysis',
  imports: [TranslocoPipe, MatButtonModule, MatCardModule],
  templateUrl: './monthly-analysis.html',
  styleUrl: './monthly-analysis.scss',
})
export class MonthlyAnalysis {
  private readonly analysis = inject(AnalysisService);
  private readonly language = inject(LanguageService);

  /** The months this rider actually rode; you can only ask about one of those. */
  readonly months = input.required<MonthlyAggregate[]>();

  /** Newest first — the month a rider wants to read about is nearly always the last one. */
  readonly selectable = computed(() =>
    [...this.months()].sort((a, b) => b.year - a.year || b.month - a.month));

  readonly selected = signal<{ year: number; month: number } | null>(null);

  readonly stored = signal<MonthlyAnalysisResult | null>(null);
  readonly busy = signal(false);
  readonly refusal = signal<AnalysisRefusal | null>(null);
  readonly failed = signal(false);

  /** Which language the UI is in right now, in the API's own vocabulary. */
  readonly wanted = computed<AnalysisLanguage>(() =>
    this.language.current() === 'hu' ? 'Hungarian' : 'English');

  /** True when what is on screen was written in the language the reader is not using. */
  readonly inOtherLanguage = computed(() => {
    const stored = this.stored();
    return stored !== null && stored.language !== this.wanted();
  });

  constructor() {
    // Default to the most recent month once the parent has its aggregates.
    effect(() => {
      const first = this.selectable()[0];
      if (first && this.selected() === null) {
        this.selected.set({ year: first.year, month: first.month });
      }
    });

    // Reading is free; writing is not. This only ever reads.
    effect(() => {
      const month = this.selected();
      const wanted = this.wanted();
      if (month) {
        this.load(month.year, month.month, wanted);
      }
    });
  }

  select(value: string): void {
    const [year, month] = value.split('-').map(Number);
    this.stored.set(null);
    this.refusal.set(null);
    this.selected.set({ year, month });
  }

  /** The one call that costs anything, and it happens only here. */
  generate(): void {
    const month = this.selected();
    if (!month || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.refusal.set(null);
    this.failed.set(false);
    this.analysis.write(month.year, month.month, this.wanted()).subscribe({
      next: (written) => {
        this.stored.set(written);
        this.busy.set(false);
      },
      error: (error: { status?: number; error?: { refusal?: AnalysisRefusal } }) => {
        this.busy.set(false);
        if (error.status === 409 && error.error?.refusal) {
          this.refusal.set(error.error.refusal);
        } else if (error.status === 403) {
          this.refusal.set('Unavailable');
        } else {
          this.failed.set(true);
        }
      },
    });
  }

  /** Letting one go is what lets a closed month be written again — deliberate, not a refresh. */
  remove(): void {
    const stored = this.stored();
    if (!stored) {
      return;
    }

    this.analysis.remove(stored.id).subscribe({
      next: () => this.stored.set(null),
      error: () => this.failed.set(true),
    });
  }

  /**
   * Reads the month in the reader's language, and falls back to the other one rather than treating a
   * reading that exists as though it did not. Both are plain GETs — nothing here writes.
   */
  private load(year: number, month: number, wanted: AnalysisLanguage): void {
    const other: AnalysisLanguage = wanted === 'Hungarian' ? 'English' : 'Hungarian';
    this.analysis.read(year, month, wanted).subscribe({
      next: (found) => this.stored.set(found),
      error: () =>
        this.analysis.read(year, month, other).subscribe({
          next: (found) => this.stored.set(found),
          error: () => this.stored.set(null),
        }),
    });
  }
}
