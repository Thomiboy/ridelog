import { Injectable, inject, signal } from '@angular/core';
import { switchMap } from 'rxjs/operators';
import { EMPTY } from 'rxjs';
import { RidesService } from '../api/rides.service';
import type { RestStop } from '../api/ride.models';

/**
 * What the global background map shows. Defaults to the latest ride's route; pages override it via
 * showRoute (one route) or showRoutes (several, e.g. the Statistics page's longest routes) and
 * return to the default via reset. The map draws one coloured track per entry.
 */
@Injectable({ providedIn: 'root' })
export class MapState {
  private readonly ridesService = inject(RidesService);

  readonly routes = signal<string[]>([]);

  /** Rest markers for the single displayed route; cleared for multi-route and default backgrounds. */
  readonly restStops = signal<RestStop[]>([]);

  private latest: string | null = null;
  private latestLoaded = false;

  /** Shows a single route (or clears the map when there's none), with optional rest markers. */
  showRoute(polyline: string | null | undefined, restStops: RestStop[] = []): void {
    this.routes.set(polyline ? [polyline] : []);
    this.restStops.set(polyline ? restStops : []);
  }

  /** Shows several routes at once; empty entries are dropped so no blank track is drawn. */
  showRoutes(polylines: string[]): void {
    this.routes.set(polylines.filter((p) => p.length > 0));
    this.restStops.set([]);
  }

  /** Drops the cached latest route so the next reset refetches — call after a ride is deleted. */
  invalidate(): void {
    this.latest = null;
    this.latestLoaded = false;
  }

  /** Restores the default (latest ride) route, from cache when already loaded. */
  reset(): void {
    if (this.latestLoaded) {
      this.showRoute(this.latest);
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
