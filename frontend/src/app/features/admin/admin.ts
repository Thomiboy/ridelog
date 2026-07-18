import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { AdminService } from '../../core/api/admin.service';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import type { ImportSummary, PolarStatus, SyncSummary } from '../../core/api/admin.models';

@Component({
  selector: 'app-admin',
  imports: [TranslocoPipe, DatePipe, MatButtonModule, MatCardModule],
  templateUrl: './admin.html',
  styleUrl: './admin.scss',
})
export class Admin {
  private readonly adminService = inject(AdminService);
  private readonly navigator = inject(ExternalNavigator);
  private readonly route = inject(ActivatedRoute);

  readonly status = signal<PolarStatus | null>(null);
  readonly selectedFiles = signal<File[]>([]);
  readonly importResult = signal<ImportSummary | null>(null);
  readonly syncResult = signal<SyncSummary | null>(null);
  readonly busy = signal(false);
  readonly failed = signal(false);
  readonly justLinked = signal(false);

  constructor() {
    // The Polar callback lands back here with ?polar=linked|error.
    const polar = this.route.snapshot.queryParamMap.get('polar');
    this.justLinked.set(polar === 'linked');
    this.failed.set(polar === 'error');

    this.loadStatus();
  }

  connectPolar(): void {
    this.failed.set(false);
    this.adminService.getPolarAuthorizeUrl().subscribe({
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
      this.adminService.importRides(files).subscribe({
        next: (result) => {
          this.importResult.set(result);
          this.busy.set(false);
        },
        error: () => this.fail(),
      }),
    );
  }

  syncNow(): void {
    this.run(() =>
      this.adminService.sync().subscribe({
        next: (result) => {
          this.syncResult.set(result);
          this.busy.set(false);
          this.loadStatus();
        },
        error: () => this.fail(),
      }),
    );
  }

  private loadStatus(): void {
    this.adminService.getPolarStatus().subscribe({
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
