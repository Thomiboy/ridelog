import { Injectable, computed, signal } from '@angular/core';

/** What the user picked: follow the OS, or force one. */
export type ThemePreference = 'system' | 'light' | 'dark';
export const THEME_PREFERENCES: ThemePreference[] = ['system', 'light', 'dark'];

/** The theme actually in effect. */
export type Theme = 'light' | 'dark';

/**
 * The UI theme. Defaults to following the OS ("system"), or a fixed light/dark, remembered across
 * visits. Drives the root `color-scheme` (Material M3 emits light-dark() values that follow it) and
 * exposes the resolved light/dark for consumers that can't use CSS (Chart.js colours, map tiles).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly StorageKey = 'ridelog.theme';

  // Guarded so the service also works where matchMedia is absent (non-browser test/SSR environments).
  private readonly media: Pick<MediaQueryList, 'matches' | 'addEventListener'> =
    typeof matchMedia === 'function' ? matchMedia('(prefers-color-scheme: dark)') : { matches: false, addEventListener: () => {} };
  private readonly systemDark = signal(this.media.matches);

  readonly preference = signal<ThemePreference>(this.readSaved() ?? 'system');

  /** The concrete theme in effect (system resolves via the OS). */
  readonly resolved = computed<Theme>(() => {
    const preference = this.preference();
    if (preference === 'system') {
      return this.systemDark() ? 'dark' : 'light';
    }
    return preference;
  });

  constructor() {
    this.media.addEventListener('change', (event) => this.systemDark.set(event.matches));
    this.apply();
  }

  /** Switches theme now and remembers the choice. */
  use(preference: ThemePreference): void {
    this.preference.set(preference);
    localStorage.setItem(ThemeService.StorageKey, preference);
    this.apply();
  }

  /** Sets the root color-scheme: a fixed light/dark, or "light dark" so the browser follows the OS. */
  private apply(): void {
    document.documentElement.style.colorScheme = this.preference() === 'system' ? 'light dark' : this.preference();
  }

  private readSaved(): ThemePreference | null {
    const saved = localStorage.getItem(ThemeService.StorageKey);
    return THEME_PREFERENCES.includes(saved as ThemePreference) ? (saved as ThemePreference) : null;
  }
}
