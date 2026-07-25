import { applyChartTheme, chartThemeColors, type ThemeableChart } from './chart-theme';

describe('chartThemeColors', () => {
  it('gives different text colours for light and dark themes', () => {
    expect(chartThemeColors('light').text).not.toBe(chartThemeColors('dark').text);
  });

  it('uses a white-based grid on dark and a black-based grid on light', () => {
    expect(chartThemeColors('dark').grid).toContain('255');
    expect(chartThemeColors('light').grid).toContain('0,');
  });
});

describe('applyChartTheme', () => {
  function fakeChart(): ThemeableChart {
    return {
      options: { plugins: { legend: { labels: {} } } },
      scales: { x: { options: {} }, y: { options: {} } },
    };
  }

  it('recolours every scale grid, ticks and border, plus the legend, on the live instance', () => {
    const chart = fakeChart();

    applyChartTheme(chart, { text: '#eee', grid: 'rgba(255,255,255,0.14)' });

    expect(chart.options.color).toBe('#eee');
    expect(chart.options.plugins!.legend!.labels!['color']).toBe('#eee');
    for (const scale of Object.values(chart.scales)) {
      expect(scale.options.grid!['color']).toBe('rgba(255,255,255,0.14)');
      expect(scale.options.ticks!['color']).toBe('#eee');
      expect(scale.options.border!['color']).toBe('rgba(255,255,255,0.14)');
    }
  });
});
