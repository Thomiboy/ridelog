import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { MatCardModule } from '@angular/material/card';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { DurationPipe } from '../../core/format/duration.pipe';
import type { RideSummary } from '../../core/api/ride.models';

/**
 * Everything the log has kept that is not a ride — the runs, walks and swims that have always
 * arrived from the same sources and never had anywhere to appear.
 *
 * Its own page rather than a view inside the rides list: they are siblings, and nothing is a term
 * for both (docs/adr/0004). The rides list is untouched, and the app still opens on cycling.
 */
@Component({
  selector: 'app-activities',
  imports: [RouterLink, TranslocoPipe, TranslocoDatePipe, TranslocoDecimalPipe, DurationPipe, MatCardModule],
  templateUrl: './activities.html',
  styleUrl: './activities.scss',
  providers: [DatePipe],
})
export class Activities {
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);

  readonly activities = signal<RideSummary[]>([]);
  readonly loaded = signal(false);

  constructor() {
    // The background map has nothing to do with this page, so it keeps its default rather than
    // showing a route belonging to something else.
    this.mapState.reset();

    this.ridesService.getOtherActivities(1, 100).subscribe({
      next: (page) => {
        this.activities.set(page.items);
        this.loaded.set(true);
      },
      error: () => this.loaded.set(true),
    });
  }
}
