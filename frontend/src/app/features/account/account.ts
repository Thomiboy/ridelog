import { Component, inject, signal } from '@angular/core';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { AccountService } from '../../core/api/account.service';
import { MapState } from '../../core/map/map-state';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import type {
  ImportSummary,
  PolarStatus,
  ReprocessSummary,
  SyncSummary,
  WeatherTopUpSummary,
} from '../../core/api/account.models';

@Component({
  selector: 'app-account',
  imports: [TranslocoPipe, TranslocoDatePipe, MatButtonModule, MatCardModule],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class Account {
  private readonly accountService = inject(AccountService);
  private readonly navigator = inject(ExternalNavigator);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);
  private readonly mapState = inject(MapState);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Only the bulk import is an admin's to run; everything else here is the rider's own log. */
  readonly isAdmin = this.auth.isAdmin;

  /** Set when the API refused to close this account because it is the public log. */
  readonly closeRefused = signal(false);

  readonly status = signal<PolarStatus | null>(null);
  readonly selectedFiles = signal<File[]>([]);
  readonly importResult = signal<ImportSummary | null>(null);
  readonly syncResult = signal<SyncSummary | null>(null);
  readonly reprocessResult = signal<ReprocessSummary | null>(null);
  readonly weatherResult = signal<WeatherTopUpSummary | null>(null);
  readonly deletedCount = signal<number | null>(null);
  readonly maxHeartRate = signal<number | null>(null);
  readonly settingsSaved = signal(false);
  readonly busy = signal(false);
  readonly failed = signal(false);
  readonly justLinked = signal(false);

  constructor() {
    // The Polar callback lands back here with ?polar=linked|error.
    const polar = this.route.snapshot.queryParamMap.get('polar');
    this.justLinked.set(polar === 'linked');
    this.failed.set(polar === 'error');

    this.loadStatus();
    this.accountService
      .getSettings()
      .subscribe((settings) => this.maxHeartRate.set(settings.maxHeartRate));
  }

  onMaxHrInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.maxHeartRate.set(value === '' ? null : Number(value));
    this.settingsSaved.set(false);
  }

  saveSettings(): void {
    this.run(() =>
      this.accountService.updateSettings({ maxHeartRate: this.maxHeartRate() }).subscribe({
        next: () => {
          this.settingsSaved.set(true);
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  connectPolar(): void {
    this.failed.set(false);
    this.accountService.getPolarAuthorizeUrl().subscribe({
      next: (r) => this.navigator.navigate(r.authorizeUrl),
      error: () => this.failed.set(true),
    });
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFiles.set(input.files ? Array.from(input.files) : []);
  }

  importRides(): void {
    const files = this.selectedFiles();
    if (files.length === 0) {
      return;
    }
    this.run(() =>
      this.accountService.importRides(files).subscribe({
        next: (result) => {
          this.importResult.set(result);
          this.ridesChanged();
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  syncNow(): void {
    this.run(() =>
      this.accountService.sync().subscribe({
        next: (result) => {
          this.syncResult.set(result);
          this.ridesChanged();
          this.busy.set(false);
          this.loadStatus();
        },
        error: () => this.fail(),
      }),
    );
  }

  reprocess(): void {
    this.run(() =>
      this.accountService.reprocess().subscribe({
        next: (result) => {
          this.reprocessResult.set(result);
          this.ridesChanged();
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  /** Fetches weather for rides still missing it, without waiting for tomorrow's sync. */
  topUpWeather(): void {
    this.run(() =>
      this.accountService.topUpWeather().subscribe({
        next: (result) => {
          this.weatherResult.set(result);
          this.ridesChanged();
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  deleteAllRides(): void {
    // Destructive and unrecoverable for Polar-synced rides (AccessLink never re-serves them),
    // so require two explicit confirmations before calling the API.
    if (!confirm(this.transloco.translate('account.maintenance.deleteConfirm1'))) {
      return;
    }
    if (!confirm(this.transloco.translate('account.maintenance.deleteConfirm2'))) {
      return;
    }
    this.run(() =>
      this.accountService.deleteAllRides().subscribe({
        next: (result) => {
          this.deletedCount.set(result.deleted);
          this.ridesChanged();
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  /**
   * Leaving, which is not the same act as emptying the log — so it asks separately, and twice,
   * because a closed account cannot be reopened and its rides do not come back from Polar.
   */
  closeAccount(): void {
    if (!confirm(this.transloco.translate('account.close.confirm1'))) {
      return;
    }
    if (!confirm(this.transloco.translate('account.close.confirm2'))) {
      return;
    }

    this.run(() =>
      this.accountService.closeAccount().subscribe({
        next: () => {
          this.auth.logout();
          this.busy.set(false);
          this.router.navigateByUrl('/');
        },
        // 409 is the API refusing because this rider is the configured public log — a specific
        // thing the owner can act on, so it does not deserve the generic failure message.
        error: (error: { status?: number }) =>
          error.status === 409 ? this.refusedAsPublicLog() : this.fail(),
      }),
    );
  }

  private refusedAsPublicLog(): void {
    this.closeRefused.set(true);
    this.busy.set(false);
  }

  /**
   * The background maps (the Rides coverage layer and the latest-ride default) are cached for the
   * session, so every operation that adds, rebuilds or removes rides has to drop those caches.
   */
  private ridesChanged(): void {
    this.mapState.invalidate();
  }

  private loadStatus(): void {
    this.accountService.getPolarStatus().subscribe({
      next: (status) => this.status.set(status),
      error: () => this.status.set(null),
    });
  }

  private run(action: () => unknown): void {
    this.failed.set(false);
    this.busy.set(true);
    action();
  }

  private fail(): void {
    this.busy.set(false);
    this.failed.set(true);
  }
}
