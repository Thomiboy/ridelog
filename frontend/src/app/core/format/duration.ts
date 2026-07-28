/** Localized unit labels and spacing for {@link formatDuration}. */
export interface DurationUnits {
  hours: string;
  minutes: string;
  /** Whether to put a space between the number and its unit (English: no, Hungarian: yes). */
  space: boolean;
}

const DEFAULT_UNITS: DurationUnits = { hours: 'h', minutes: 'm', space: false };

/** Formats a whole number of minutes as `1h 58m` / `1h` / `45m`, localizing the units (`1 ó 58 p`). */
export function formatDuration(minutes: number, units: DurationUnits = DEFAULT_UNITS): string {
  const hours = Math.floor(minutes / 60);
  const mins = Math.round(minutes % 60);
  const sep = units.space ? ' ' : '';
  const h = `${hours}${sep}${units.hours}`;
  const m = `${mins}${sep}${units.minutes}`;
  if (hours === 0) {
    return m;
  }
  return mins === 0 ? h : `${h} ${m}`;
}
