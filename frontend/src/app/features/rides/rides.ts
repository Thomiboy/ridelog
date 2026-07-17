import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { RidesService } from '../../core/api/rides.service';
import type { Paged, RideSummary } from '../../core/api/ride.models';

@Component({
  selector: 'app-rides',
  imports: [RouterLink, TranslocoPipe, DatePipe, DecimalPipe],
  templateUrl: './rides.html',
  styleUrl: './rides.scss',
})
export class Rides {
  private readonly ridesService = inject(RidesService);

  readonly result = signal<Paged<RideSummary> | null>(null);

  readonly hasPrev = computed(() => (this.result()?.page ?? 1) > 1);
  readonly hasNext = computed(() => {
    const result = this.result();
    return result !== null && result.page * result.pageSize < result.total;
  });

  constructor() {
    this.load();
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
