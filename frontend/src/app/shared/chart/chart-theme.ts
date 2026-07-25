import type { Theme } from '../../core/theme/theme.service';

/** Text and gridline colours for Chart.js, matching the app theme. */
export interface ChartThemeColors {
  text: string;
  grid: string;
}

/** Chart.js text/grid colours for the theme (dark uses light text on a faint white grid). */
export function chartThemeColors(theme: Theme): ChartThemeColors {
  return theme === 'dark'
    ? { text: '#e3e2e6', grid: 'rgba(255, 255, 255, 0.14)' }
    : { text: '#1a1c1e', grid: 'rgba(0, 0, 0, 0.10)' };
}

/** The parts of a live Chart.js instance we retheme — a structural slice so this stays unit-testable. */
export interface ThemeableChart {
  options: { color?: unknown; plugins?: { legend?: { labels?: Record<string, unknown> } } };
  scales: Record<string, { options: { grid?: Record<string, unknown>; ticks?: Record<string, unknown>; border?: Record<string, unknown> } }>;
}

/**
 * Recolours a live chart's axes, gridlines, ticks and legend to the theme. Global Chart.js defaults
 * only reach charts built after they change, so switching theme has to mutate existing instances
 * directly — otherwise the chart keeps its old colours until the next full re-render (page refresh).
 */
export function applyChartTheme(chart: ThemeableChart, colors: ChartThemeColors): void {
  const { text, grid } = colors;
  chart.options.color = text;
  const labels = chart.options.plugins?.legend?.labels;
  if (labels) {
    labels['color'] = text;
  }
  for (const scale of Object.values(chart.scales)) {
    (scale.options.grid ??= {})['color'] = grid;
    (scale.options.ticks ??= {})['color'] = text;
    (scale.options.border ??= {})['color'] = grid;
  }
}
