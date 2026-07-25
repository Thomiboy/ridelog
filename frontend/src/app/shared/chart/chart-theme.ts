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
