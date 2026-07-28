import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { Component, computed, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import type { RideSummary } from '../../core/api/ride.models';

/**
 * A searchable panel over all cycling rides for picking the ride to compare against. Filters by date
 * or distance; excludes the current ride. Emits the chosen ride, or close to dismiss.
 */
@Component({
  selector: 'app-ride-picker',
  imports: [TranslocoPipe, TranslocoDatePipe, TranslocoDecimalPipe, MatButtonModule, MatIconModule],
  templateUrl: './ride-picker.html',
  styleUrl: './ride-picker.scss',
})
export class RidePicker {
  readonly rides = input<RideSummary[]>([]);
  readonly excludeId = input<string>('');

  readonly pick = output<RideSummary>();
  readonly close = output<void>();

  readonly query = signal('');

  readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.rides()
      .filter((ride) => ride.id !== this.excludeId())
      .filter((ride) => q === '' || `${ride.startTime} ${ride.distanceKm}`.toLowerCase().includes(q));
  });

  search(value: string): void {
    this.query.set(value);
  }

  select(ride: RideSummary): void {
    this.pick.emit(ride);
  }

  dismiss(): void {
    this.close.emit();
  }
}
