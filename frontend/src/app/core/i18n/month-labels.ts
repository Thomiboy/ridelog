/** English fallback month abbreviations, used as the default when no locale is threaded in. */
export const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** Short month names Jan–Dec for a locale (e.g. en → "Jan", hu → "jan."), for chart axes. */
export function monthLabels(locale: string): string[] {
  const format = new Intl.DateTimeFormat(locale, { month: 'short' });
  return Array.from({ length: 12 }, (_, month) => format.format(new Date(2024, month, 1)));
}
