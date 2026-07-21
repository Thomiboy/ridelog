import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { formatDuration } from '../../core/format/duration';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';

@Component({
  selector: 'app-ride-detail',
  imports: [TranslocoPipe, DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule, MatCardModule],
  templateUrl: './ride-detail.html',
  styleUrl: './ride-detail.scss',
})
export class RideDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly sheetState = inject(SheetState);

  readonly ride = signal<RideDetailDto | null>(null);

  /** Exposed for the template: renders `durationMinutes` as `1h 58m`. */
  readonly formatDuration = formatDuration;

  goToPrevious(): void {
    this.step(this.ride()?.previousId);
  }

  goToNext(): void {
    this.step(this.ride()?.nextId);
  }

  private step(id: string | null | undefined): void {
    if (id) {
      this.router.navigateByUrl(`/rides/${id}`);
    }
  }

  constructor() {
    // React to every id change (the stepper navigates between /rides/:id without recreating this
    // component, so reading the snapshot once would leave the page and map on the old ride).
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        return;
      }
      // Snap to half so the selected ride's route stays visible on the background map.
      this.sheetState.request('half');
      this.ridesService.getRide(id).subscribe((ride) => {
        this.ride.set(ride);
        // The route draws on the global background map instead of an embedded one.
        this.mapState.showRoute(ride.routePolyline);
      });
    });
  }
}
