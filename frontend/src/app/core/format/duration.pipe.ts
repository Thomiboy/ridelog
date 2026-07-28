import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { formatDuration } from './duration';

/**
 * Renders a minute count as a localized duration (`1h 58m` / `1 ó 58 p`), reading the unit labels
 * from the active translation. Impure so it re-renders on language change (Transloco marks the view
 * dirty). English joins number and unit tightly; other languages add a space, per typographic norm.
 */
@Pipe({ name: 'duration', pure: false })
export class DurationPipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);

  transform(minutes: number): string {
    return formatDuration(minutes, {
      hours: this.transloco.translate('format.durationHours'),
      minutes: this.transloco.translate('format.durationMinutes'),
      space: this.transloco.getActiveLang() !== 'en',
    });
  }
}
