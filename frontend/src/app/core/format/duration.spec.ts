import { formatDuration } from './duration';

describe('formatDuration', () => {
  it('splits minutes into hours and minutes', () => {
    expect(formatDuration(118)).toBe('1h 58m');
    expect(formatDuration(125)).toBe('2h 5m');
  });

  it('omits the minutes when the duration is a whole number of hours', () => {
    expect(formatDuration(60)).toBe('1h');
  });

  it('shows only minutes under an hour', () => {
    expect(formatDuration(45)).toBe('45m');
    expect(formatDuration(0)).toBe('0m');
  });

  it('uses the given localized units and spacing (Hungarian)', () => {
    const hu = { hours: 'ó', minutes: 'p', space: true };
    expect(formatDuration(118, hu)).toBe('1 ó 58 p');
    expect(formatDuration(60, hu)).toBe('1 ó');
    expect(formatDuration(45, hu)).toBe('45 p');
  });
});
