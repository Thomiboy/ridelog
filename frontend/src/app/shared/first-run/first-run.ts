import { Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * What a page shows before there is anything to show.
 *
 * A new rider's log is genuinely empty and stays that way until Polar delivers something, so zeros
 * and blank charts would read as a broken app rather than a new one. The same piece serves the
 * dashboard, the statistics and the rides list, because they are all empty for the same reason.
 */
@Component({
  selector: 'app-first-run',
  imports: [TranslocoPipe],
  template: `
    <div class="first-run">
      <p class="headline">{{ 'firstRun.title' | transloco }}</p>
      <!-- Only the rider whose log this is can do anything about it; a visitor is just looking. -->
      @if (ownLog()) {
        <p>{{ 'firstRun.horizon' | transloco }}</p>
      }
    </div>
  `,
  styleUrl: './first-run.scss',
})
export class FirstRun {
  /** Whether the empty log belongs to whoever is reading it. */
  readonly ownLog = input(false);
}
