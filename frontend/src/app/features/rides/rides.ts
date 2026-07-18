import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import type { Paged, RideSummary } from '../../core/api/ride.models';

@Component({
  selector: 'app-rides',
  imports: [RouterLink, TranslocoPipe, DatePipe, DecimalPipe, MatButtonModule],
  templateUrl: './rides.html',
  styleUrl: './rides.scss',
})
export class Rides {
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly router = inject(Router);

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
