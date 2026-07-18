import { Injectable, inject, signal } from '@angular/core';
import { switchMap } from 'rxjs/operators';
import { EMPTY } from 'rxjs';
import { RidesService } from '../api/rides.service';

/**
 * What the global background map shows. Defaults to the latest ride's route;
 * pages (e.g. ride selection) override it via showRoute and return to the
 * default via reset.
 */
@Injectable({ providedIn: 'root' })
export class MapState {
  private readonly ridesService = inject(RidesService);

  readonly polyline = signal<string | null>(null);

  private latest: string | null = null;
  private latestLoaded = false;

  showRoute(polyline: string | null | undefined): void {
    this.polyline.set(polyline ?? null);
  }

  /** Drops the cached latest route so the next reset refetches — call after a ride is deleted. */
  invalidate(): void {
    this.latest = null;
    this.latestLoaded = false;
  }

  /** Restores the default (latest ride) route, from cache when already loaded. */
  reset(): void {
    if (this.latestLoaded) {
      this.polyline.set(this.latest);
    } else {
      this.loadLatest();
    }
  }

  /** Loads the newest ride's route as the default background (no-op when there are no rides). */
  loadLatest(): void {
    this.ridesService
      .getRides(1, 1)
      .pipe(switchMap((page) => (page.items.length > 0 ? this.ridesService.getRide(page.items[0].id) : EMPTY)))
      .subscribe({
        next: (ride) => {
          this.latest = ride.routePolyline ?? null;
          this.latestLoaded = true;
          this.showRoute(this.latest);
        },
        error: () => this.showRoute(null),
        complete: () => {
          // EMPTY (no rides) completes without next: remember that "nothing" is the default.
          if (!this.latestLoaded) {
            this.latest = null;
            this.latestLoaded = true;
            this.showRoute(null);
          }
        },
      });
  }
}
