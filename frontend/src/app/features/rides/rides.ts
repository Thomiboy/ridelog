import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { DurationPipe } from '../../core/format/duration.pipe';
import { LanguageService } from '../../core/i18n/language.service';
import { FirstRun } from '../../shared/first-run/first-run';
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
    TranslocoDatePipe,
    TranslocoDecimalPipe,
    DurationPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    SourceChips,
    FirstRun,
  ],
  templateUrl: './rides.html',
  styleUrl: './rides.scss',
})
export class Rides implements OnDestroy {
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly sheetState = inject(SheetState);
  private readonly viewState = inject(RidesViewState);
  private readonly language = inject(LanguageService);

  readonly isLoggedIn = this.auth.isLoggedIn;
  readonly pageSize = computed(() => PAGE_SIZE[this.sheetState.current()] ?? 9);

  readonly result = signal<Paged<RideSummary> | null>(null);

  /** List (paged table) or the monthly calendar; kept across navigation. */
  readonly view = this.viewState.view;

  /** Every ride for the calendar; null until the calendar view is first opened. */
  readonly allRides = signal<RideSummary[] | null>(null);

  private readonly today = new Date();
  readonly calendarYear = signal(this.today.getFullYear());
  readonly calendarMonth = signal(this.today.getMonth() + 1);

  /** The multi-ride day whose rides are listed in the panel; null when none is chosen. */
  readonly selectedDay = signal<CalendarDay | null>(null);

  /** The displayed month as a Date, for the localized month label. */
  readonly calendarDate = computed(() => new Date(this.calendarYear(), this.calendarMonth() - 1, 1));

  /** Month + year heading in the active language (e.g. "July 2026" / "2026. július"). */
  readonly calendarLabel = computed(() =>
    new Intl.DateTimeFormat(this.language.current(), { month: 'long', year: 'numeric' }).format(this.calendarDate()),
  );

  readonly calendar = computed(() => {
    const rides = this.allRides();
    return rides ? buildCalendarMonth(rides, this.calendarYear(), this.calendarMonth()) : null;
  });

  /**
   * Monday-first weekday headers in the active language (Jan 1 2024 was a Monday). A computed so they
   * re-translate when the language switches — a plain field would freeze the startup locale.
   */
  readonly weekdayLabels = computed(() =>
    Array.from({ length: 7 }, (_, i) =>
      new Intl.DateTimeFormat(this.language.current(), { weekday: 'short' }).format(new Date(2024, 0, 1 + i)),
    ),
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
    // The whole page sits over a coverage map of every route ridden — the "where have I been" view.
    this.mapState.showAllRoutes();
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

  ngOnDestroy(): void {
    // Leaving Rides hands the background back to the latest ride, so the coverage layer doesn't
    // linger on the Dashboard.
    this.mapState.reset();
  }

  private pageFromUrl(): number {
    const raw = Number(this.route.snapshot.queryParamMap.get('page'));
    return Number.isInteger(raw) && raw > 0 ? raw : 1;
  }

  setView(view: RidesView): void {
    this.view.set(view);
    this.applyView(view);
  }

  /** Runs a view's side effects: the calendar expands the sheet and loads its rides once. */
  private applyView(view: RidesView): void {
    if (view === 'list') {
      this.sheetState.request('half');
      return;
    }
    this.sheetState.request('full');
    if (this.allRides() === null) {
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
