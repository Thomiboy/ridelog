import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import type { Paged, RideSummary } from '../../core/api/ride.models';

// How many rides fit without scrolling at each sheet height (collapsed isn't for browsing).
const PAGE_SIZE: Record<string, number> = { full: 18, half: 9, collapsed: 9 };

@Component({
  selector: 'app-rides',
  imports: [RouterLink, TranslocoPipe, DatePipe, DecimalPipe, MatButtonModule, MatIconModule],
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

  readonly isLoggedIn = this.auth.isLoggedIn;
  readonly pageSize = computed(() => PAGE_SIZE[this.sheetState.current()] ?? 9);

  readonly result = signal<Paged<RideSummary> | null>(null);

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
