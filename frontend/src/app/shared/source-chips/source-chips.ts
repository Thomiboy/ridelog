import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** Maps a backend source token to its Transloco label key. */
const LABEL_KEYS: Record<string, string> = {
  PolarAutoSync: 'rides.source.polarAutoSync',
  PolarImport: 'rides.source.polarImport',
  Bryton: 'rides.source.bryton',
};

/** Renders a ride's source tokens as localized chips (Polar · Auto-sync, Polar · Import, Bryton). */
@Component({
  selector: 'app-source-chips',
  imports: [TranslocoPipe],
  template: `@for (token of sources(); track token) {
    <span class="source-chip" data-source-chip>{{ labelKey(token) | transloco }}</span>
  }`,
  styleUrl: './source-chips.scss',
})
export class SourceChips {
  readonly sources = input.required<string[]>();

  // Unknown tokens fall back to the raw value so nothing silently disappears.
  labelKey(token: string): string {
    return LABEL_KEYS[token] ?? token;
  }
}
