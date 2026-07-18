import { Injectable, signal } from '@angular/core';
import type { SnapState } from './snap';

/** Lets pages request a sheet snap (e.g. selecting a ride snaps to half so the route is visible). */
@Injectable({ providedIn: 'root' })
export class SheetState {
  readonly requested = signal<SnapState | null>(null);

  request(state: SnapState): void {
    this.requested.set(state);
  }

  clear(): void {
    this.requested.set(null);
  }
}
