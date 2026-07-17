import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AdminService } from '../../core/api/admin.service';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import type { ImportSummary, SyncSummary } from '../../core/api/admin.models';

@Component({
  selector: 'app-admin',
  imports: [TranslocoPipe],
  templateUrl: './admin.html',
  styleUrl: './admin.scss',
})
export class Admin {
  private readonly adminService = inject(AdminService);
  private readonly navigator = inject(ExternalNavigator);

  readonly selectedFiles = signal<File[]>([]);
  readonly importResult = signal<ImportSummary | null>(null);
  readonly syncResult = signal<SyncSummary | null>(null);
  readonly busy = signal(false);

  connectPolar(): void {
    this.adminService.getPolarAuthorizeUrl().subscribe((r) => this.navigator.navigate(r.authorizeUrl));
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
    this.busy.set(true);
    this.adminService.importRides(files).subscribe({
      next: (result) => {
        this.importResult.set(result);
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }

  syncNow(): void {
    this.busy.set(true);
    this.adminService.sync().subscribe({
      next: (result) => {
        this.syncResult.set(result);
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }
}
