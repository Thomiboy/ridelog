/** Formats a whole number of minutes as `1h 58m` / `1h` / `45m`. */
export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const mins = Math.round(minutes % 60);
  if (hours === 0) {
    return `${mins}m`;
  }
  return mins === 0 ? `${hours}h` : `${hours}h ${mins}m`;
}
