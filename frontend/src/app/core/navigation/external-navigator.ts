import { Injectable } from '@angular/core';

/** Thin wrapper around full-page navigation so components stay testable. */
@Injectable({ providedIn: 'root' })
export class ExternalNavigator {
  navigate(url: string): void {
    window.location.href = url;
  }
}
