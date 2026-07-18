import { Injectable, inject, signal } from '@angular/core';
import { switchMap } from 'rxjs/operators';
import { EMPTY } from 'rxjs';
import { RidesService } from '../api/rides.service';

/**
 * What the global background map shows. Defaults to the latest ride's route;
 * pages (e.g. ride selection) override it via showRoute.
 */
@Injectable({ providedIn: 'root' })
export class MapState {
  private readonly ridesService = inject(RidesService);

  readonly polyline = signal<string | null>(null);

  showRoute(polyline: string | null | undefined): void {
    this.polyline.set(polyline ?? null);
  }

  /** Loads the newest ride's route as the default background (no-op when there are no rides). */
  loadLatest(): void {
    this.ridesService
      .getRides(1, 1)
      .pipe(switchMap((page) => (page.items.length > 0 ? this.ridesService.getRide(page.items[0].id) : EMPTY)))
      .subscribe({
        next: (ride) => this.showRoute(ride.routePolyline),
        error: () => this.showRoute(null),
      });
  }
}
