import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { RouteMap } from '../ride-detail/route-map/route-map';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { formatDuration } from '../../core/format/duration';
import { SourceChips } from '../../shared/source-chips/source-chips';
import { buildCalendarMonth, type CalendarDay } from './rides-calendar';
import { RidesViewState, type RidesView } from './rides-view-state';
import type { Paged, RideSummary } from '../../core/api/ride.models';

// How many rides fit without scrolling at each sheet height (collapsed isn't for browsing).
const PAGE_SIZE: Record<string, number> = { full: 18, half: 8, collapsed: 8 };

@Component({
  selector: 'app-rides',
  imports: [
    TranslocoPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    SourceChips,
    RouteMap,
  ],
  templateUrl: './rides.html',
  styleUrl: './rides.scss',
})
export class Rides {
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly sheetState = inject(SheetState);
  private readonly viewState = inject(RidesViewState);

  readonly isLoggedIn = this.auth.isLoggedIn;
  readonly pageSize = computed(() => PAGE_SIZE[this.sheetState.current()] ?? 9);

  /** Exposed for the template: renders `durationMinutes` as `1h 58m`. */
  readonly formatDuration = formatDuration;

  readonly result = signal<Paged<RideSummary> | null>(null);

  /** List (paged table), the all-routes coverage map, or the monthly calendar; kept across navigation. */
  readonly view = this.viewState.view;

  /** Every route's polyline for the coverage map; null until the map view is first opened. */
  readonly mapRoutes = signal<string[] | null>(null);

  /** Every ride for the calendar; null until the calendar view is first opened. */
  readonly allRides = signal<RideSummary[] | null>(null);

  private readonly today = new Date();
  readonly calendarYear = signal(this.today.getFullYear());
  readonly calendarMonth = signal(this.today.getMonth() + 1);

  /** The multi-ride day whose rides are listed in the panel; null when none is chosen. */
  readonly selectedDay = signal<CalendarDay | null>(null);

  /** The displayed month as a Date, for the localized month label. */
  readonly calendarDate = computed(() => new Date(this.calendarYear(), this.calendarMonth() - 1, 1));

  readonly calendar = computed(() => {
    const rides = this.allRides();
    return rides ? buildCalendarMonth(rides, this.calendarYear(), this.calendarMonth()) : null;
  });

  /** Monday-first localized weekday headers (Jan 1 2024 was a Monday). */
  readonly weekdayLabels = Array.from({ length: 7 }, (_, i) =>
    new Intl.DateTimeFormat(undefined, { weekday: 'short' }).format(new Date(2024, 0, 1 + i)),
  );

  private currentPage = 1;
  private snapInitialised = false;

  readonly hasPrev = computed(() => (this.result()?.page ?? 1) > 1);
  readonly hasNext = computed(() => {
    const result = this.result();
    return result !== null && result.page * result.pageSize < result.total;
  });
  readonly totalPages = computed(() => {
    const result = this.result();
    return result === null ? 1 : Math.max(1, Math.ceil(result.total / result.pageSize));
  });

  constructor() {
    // Returning to the list swaps the background map back to the latest ride.
    this.mapState.reset();
    // Restore the page from the URL so returning from a ride's detail keeps your place.
    this.load(this.pageFromUrl());
    // Apply the remembered view (calendar by default), loading its data and sizing the sheet.
    this.applyView(this.view());

    // Re-fetch when the sheet snap (and thus page size) changes. Snap states are discrete, so this
    // only fires on snap transitions, not during a drag. Skip the first run — the constructor already
    // loaded — and stay on the current page.
    effect(() => {
      this.pageSize();
      if (this.snapInitialised) {
        this.load(this.currentPage);
      }
      this.snapInitialised = true;
    });
  }

  private pageFromUrl(): number {
    const raw = Number(this.route.snapshot.queryParamMap.get('page'));
    return Number.isInteger(raw) && raw > 0 ? raw : 1;
  }

  setView(view: RidesView): void {
    this.view.set(view);
    this.applyView(view);
  }

  /** Runs a view's side effects: the map/calendar expand the sheet and load their data once. */
  private applyView(view: RidesView): void {
    if (view === 'list') {
      this.sheetState.request('half');
      return;
    }
    this.sheetState.request('full');
    if (view === 'map' && this.mapRoutes() === null) {
      this.ridesService.getAllRoutes().subscribe((routes) => this.mapRoutes.set(routes.map((r) => r.routePolyline)));
    }
    if (view === 'calendar' && this.allRides() === null) {
      this.ridesService.getAllRides().subscribe((rides) => this.allRides.set(rides));
    }
  }

  prevMonth(): void {
    this.shiftMonth(-1);
  }

  nextMonth(): void {
    this.shiftMonth(1);
  }

  private shiftMonth(delta: number): void {
    const shifted = new Date(this.calendarYear(), this.calendarMonth() - 1 + delta, 1);
    this.calendarYear.set(shifted.getFullYear());
    this.calendarMonth.set(shifted.getMonth() + 1);
    this.selectedDay.set(null);
  }

  /**
   * Background shade for a day cell: navy scaled by the day's relative distance; none when ride-free.
   * Alpha stays in a mid band so the busiest day is clearly darker yet the km text stays readable.
   */
  dayShade(day: CalendarDay): string {
    return day.rideCount > 0 ? `rgba(27, 58, 107, ${0.1 + day.intensity * 0.45})` : '';
  }

  openDay(day: CalendarDay): void {
    if (day.rideCount === 1) {
      this.selectedDay.set(null);
      this.open(day.rides[0]);
    } else if (day.rideCount > 1) {
      this.selectedDay.set(day);
    }
  }

  open(ride: RideSummary): void {
    this.router.navigateByUrl(`/rides/${ride.id}`);
  }

  remove(ride: RideSummary, event: Event): void {
    // The whole row navigates; keep the delete button from triggering it.
    event.stopPropagation();
    if (!confirm(this.transloco.translate('rides.deleteConfirm'))) {
      return;
    }
    this.ridesService.deleteRide(ride.id).subscribe(() => {
      // The latest ride may have changed, so drop the cached background route.
      this.mapState.invalidate();
      this.load(this.result()?.page ?? 1);
    });
  }

  load(page = 1): void {
    this.currentPage = page;
    this.ridesService.getRides(page, this.pageSize()).subscribe((result) => this.result.set(result));
  }

  private goToPage(page: number): void {
    this.load(page);
    // Keep the page in the URL so back from a ride's detail returns here, and it's bookmarkable.
    this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  prev(): void {
    if (this.hasPrev()) {
      this.goToPage(this.result()!.page - 1);
    }
  }

  next(): void {
    if (this.hasNext()) {
      this.goToPage(this.result()!.page + 1);
    }
  }
}
