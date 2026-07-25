import { monthLabels } from './month-labels';

describe('monthLabels', () => {
  it('returns twelve short month names for the locale', () => {
    const en = monthLabels('en');
    expect(en).toHaveLength(12);
    expect(en[0]).toBe('Jan');
  });

  it('localizes the month names to the requested language', () => {
    // Hungarian short months differ from English (e.g. not "Jan"/"Feb"/…).
    expect(monthLabels('hu')).not.toEqual(monthLabels('en'));
  });
});
