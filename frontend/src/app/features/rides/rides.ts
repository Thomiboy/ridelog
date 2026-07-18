import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import type { Paged, RideSummary } from '../../core/api/ride.models';

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
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);

  readonly isLoggedIn = this.auth.isLoggedIn;

  readonly result = signal<Paged<RideSummary> | null>(null);

  readonly hasPrev = computed(() => (this.result()?.page ?? 1) > 1);
  readonly hasNext = computed(() => {
    const result = this.result();
    return result !== null && result.page * result.pageSize < result.total;
  });

  constructor() {
    // Returning to the list swaps the background map back to the latest ride.
    this.mapState.reset();
    this.load();
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
    this.ridesService.getRides(page).subscribe((result) => this.result.set(result));
  }

  prev(): void {
    if (this.hasPrev()) {
      this.load(this.result()!.page - 1);
    }
  }

  next(): void {
    if (this.hasNext()) {
      this.load(this.result()!.page + 1);
    }
  }
}
