import { chartThemeColors } from './chart-theme';

describe('chartThemeColors', () => {
  it('gives different text colours for light and dark themes', () => {
    expect(chartThemeColors('light').text).not.toBe(chartThemeColors('dark').text);
  });

  it('uses a white-based grid on dark and a black-based grid on light', () => {
    expect(chartThemeColors('dark').grid).toContain('255');
    expect(chartThemeColors('light').grid).toContain('0,');
  });
});
