import { provideTranslocoLocale } from '@jsverse/transloco-locale';

/**
 * Drives number/date formatting from the active UI language: English keeps its current en-US style,
 * Hungarian uses hu-HU (comma decimals, space thousands, year-first dates). Transloco-locale maps the
 * active lang to its locale automatically, so switching language re-formats live.
 */
export const translocoLocaleProviders = provideTranslocoLocale({
  langToLocaleMapping: { en: 'en-US', hu: 'hu-HU' },
});
