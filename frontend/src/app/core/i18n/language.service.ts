import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { forkJoin, type Observable } from 'rxjs';
import type { Translation } from '@jsverse/transloco';

/** The languages the UI ships (English default, Hungarian). */
export const LANGUAGES = ['en', 'hu'] as const;
export type Language = (typeof LANGUAGES)[number];

/**
 * The active UI language, remembered across visits (localStorage). English is the default; the
 * header switcher calls use(), and startup calls init() to apply the saved choice before first paint.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private static readonly StorageKey = 'ridelog.lang';

  readonly current = signal<Language>(this.readSaved() ?? (this.transloco.getDefaultLang() as Language));

  /**
   * Applies the current language and preloads every bundle (so runtime switches — including chart
   * labels built with translate() — are instant), returning the loads so startup can await them.
   */
  init(): Observable<Translation[]> {
    this.transloco.setActiveLang(this.current());
    return forkJoin(LANGUAGES.map((lang) => this.transloco.load(lang)));
  }

  /** Switches language now and remembers the choice. */
  use(lang: Language): void {
    this.transloco.setActiveLang(lang);
    this.current.set(lang);
    localStorage.setItem(LanguageService.StorageKey, lang);
  }

  private readSaved(): Language | null {
    const saved = localStorage.getItem(LanguageService.StorageKey);
    return LANGUAGES.includes(saved as Language) ? (saved as Language) : null;
  }
}
