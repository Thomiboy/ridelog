import { Component, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { PendingRiders } from '../../core/api/pending-riders';
import { RidersService, type Approval, type RiderSummary } from '../../core/api/riders.service';
import { storageSize } from './storage-size';

/**
 * Who has knocked, and who is in. Registration is open — anyone with a Google or Microsoft account
 * can arrive — so this is where arriving becomes being let in.
 */
@Component({
  selector: 'app-riders',
  imports: [TranslocoPipe, MatButtonModule],
  templateUrl: './riders.html',
  styleUrl: './riders.scss',
})
export class Riders {
  private readonly riders = inject(RidersService);
  private readonly pending = inject(PendingRiders);

  readonly all = signal<RiderSummary[]>([]);
  readonly busy = signal(false);

  /** The API's own sentence when it refuses — it names which refusal, which a generic one cannot. */
  readonly refusal = signal<string | null>(null);

  constructor() {
    this.load();
  }

  /** Bytes as a person reads them; the raw count answers the question in the wrong unit. */
  readonly size = storageSize;

  makePublic(riderId: string): void {
    this.act(this.riders.setPublicLog(riderId));
  }

  set(riderId: string, approval: Approval): void {
    this.act(this.riders.setApproval(riderId, approval));
  }

  /** Every control on this page does the same thing afterwards: reload, and show a refusal as given. */
  private act(change: Observable<void>): void {
    this.busy.set(true);
    this.refusal.set(null);
    change.subscribe({
      next: () => {
        // Reload rather than patch the row: the list then shows what the server believes.
        this.busy.set(false);
        this.load();
        // The badge counts what this page just changed, so it follows rather than going stale.
        this.pending.refreshPending();
      },
      error: (error: { status?: number; error?: string }) => {
        this.busy.set(false);
        this.refusal.set(error.status === 409 && error.error ? error.error : null);
      },
    });
  }

  private load(): void {
    this.riders.list().subscribe((riders) => this.all.set(riders));
  }
}
