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

  /**
   * Where on each route the reader is currently pointing, held parallel to routes: entry i belongs
   * to route i, so a comparison marks both rides at the same place and each marker can take its
   * route's colour. Empty when nothing is being pointed at.
   */
  readonly highlights = signal<[number, number][]>([]);

  /**
   * Whether the routes are one translucent coverage layer (the Rides "where have I been" backdrop)
   * rather than distinct coloured tracks. Route count can't imply this — Statistics deliberately
   * draws three routes in distinct colours — so the intent travels with the state.
   */
  readonly coverage = signal(false);

  private latest: string | null = null;
  private latestLoaded = false;

  private allRoutes: string[] = [];
  private allRoutesLoaded = false;

  /** Shows a single route (or clears the map when there's none), with optional rest markers. */
  showRoute(polyline: string | null | undefined, restStops: RestStop[] = []): void {
    this.routes.set(polyline ? [polyline] : []);
    this.restStops.set(polyline ? restStops : []);
    this.highlights.set([]);
    this.coverage.set(false);
  }

  /** Shows several routes at once as distinct tracks; empty entries are dropped so no blank track is drawn. */
  showRoutes(polylines: string[]): void {
    this.routes.set(polylines.filter((p) => p.length > 0));
    this.restStops.set([]);
    this.highlights.set([]);
    this.coverage.set(false);
  }

  /** Points at a place on each displayed route; an empty list means nothing is being pointed at. */
  highlight(positions: [number, number][]): void {
    this.highlights.set(positions);
  }

  /** Shows every route as one translucent coverage layer, so frequently-ridden roads darken. */
  showCoverage(polylines: string[]): void {
    this.showRoutes(polylines);
    this.coverage.set(true);
  }

  /**
   * Paints every ride's route as the coverage backdrop (the Rides page). The route list is the
   * largest payload in the app and Rides ⇄ ride detail is a frequent round trip, so it's fetched
   * once per session and reused until invalidate().
   */
  showAllRoutes(): void {
    if (this.allRoutesLoaded) {
      this.showCoverage(this.allRoutes);
      return;
    }
    this.ridesService.getAllRoutes().subscribe({
      next: (routes) => {
        this.allRoutes = routes.map((route) => route.routePolyline);
        this.allRoutesLoaded = true;
        this.showCoverage(this.allRoutes);
      },
      error: () => this.showCoverage([]),
    });
  }

  /**
   * Drops both cached backgrounds so the next reset / showAllRoutes refetches — call whenever the
   * set of rides changes (a delete, but also an import, a sync or a delete-all).
   */
  invalidate(): void {
    this.latest = null;
    this.latestLoaded = false;
    this.allRoutes = [];
    this.allRoutesLoaded = false;
  }

  /** Restores the default (latest ride) route, from cache when already loaded. */
  reset(): void {
    // Dropped up front rather than left to whatever this ends up showing: loading the default
    // background is a request away, and a marker from the page being left must not outlive it.
    this.highlights.set([]);

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
