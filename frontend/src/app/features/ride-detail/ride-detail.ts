import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';

@Component({
  selector: 'app-ride-detail',
  imports: [TranslocoPipe, DatePipe, DecimalPipe, RouterLink, MatButtonModule, MatIconModule],
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
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      // Snap to half so the selected ride's route stays visible on the background map.
      this.sheetState.request('half');
      this.ridesService.getRide(id).subscribe((ride) => {
        this.ride.set(ride);
        // The route draws on the global background map instead of an embedded one.
        this.mapState.showRoute(ride.routePolyline);
      });
    }
  }
}
