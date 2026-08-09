import { Injectable, effect, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { RidersService } from './riders.service';

/**
 * How many riders are waiting on the owner.
 *
 * Nothing in this app sends email, so a count in the navigation is the only notification it can
 * honestly give — and without one the approval queue is a box nobody opens, which turns the gate
 * into "nobody ever gets in". Fetched once when an admin arrives and refreshed after the owner acts,
 * rather than polled: this runs on a free tier where waking the database is the expensive part.
 */
@Injectable({ providedIn: 'root' })
export class PendingRiders {
  private readonly riders = inject(RidersService);
  private readonly auth = inject(AuthService);

  private readonly _pending = signal(0);
  readonly pending = this._pending.asReadonly();

  constructor() {
    effect(() => {
      if (this.auth.isAdmin()) {
        this.refreshPending();
      } else {
        this._pending.set(0);
      }
    });
  }

  refreshPending(): void {
    this.riders.list().subscribe({
      next: (riders) => this._pending.set(riders.filter((rider) => rider.approval === 'Pending').length),
      // A count nobody asked for must never be the thing that breaks a page.
      error: () => this._pending.set(0),
    });
  }
}
