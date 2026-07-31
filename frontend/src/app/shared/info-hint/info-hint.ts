import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * A small "where did this number come from" marker beside a card title.
 *
 * Several of the figures on a ride are honest only with a caveat — measured on the bike versus
 * reported for an area, a device's own summary versus something derived from GPS. Those caveats
 * matter but do not earn a line of their own in a card, so they live one gesture away.
 *
 * Touch gestures are on and the text doubles as the label: an icon whose entire content sits behind
 * hover is otherwise silent to a screen reader and unreachable without a mouse.
 */
@Component({
  selector: 'app-info-hint',
  imports: [MatIconModule, MatTooltipModule, TranslocoPipe],
  template: `<mat-icon
    class="info-hint"
    tabindex="0"
    data-info-hint
    [attr.aria-label]="key() | transloco"
    [matTooltip]="key() | transloco"
    matTooltipPosition="above"
    matTooltipTouchGestures="on"
    >info_outline</mat-icon
  >`,
  styleUrl: './info-hint.scss',
})
export class InfoHint {
  /** Transloco key of the caveat to show. */
  readonly key = input.required<string>();
}
