import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { RidesService } from '../../core/api/rides.service';
import type { RideDetail as RideDetailDto } from '../../core/api/ride.models';
import { RouteMap } from './route-map/route-map';

@Component({
  selector: 'app-ride-detail',
  imports: [RouteMap, TranslocoPipe, DatePipe, DecimalPipe],
  templateUrl: './ride-detail.html',
  styleUrl: './ride-detail.scss',
})
export class RideDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly ridesService = inject(RidesService);

  readonly ride = signal<RideDetailDto | null>(null);

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.ridesService.getRide(id).subscribe((ride) => this.ride.set(ride));
    }
  }
}
