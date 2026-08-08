import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { Account } from './account';
import { AccountService } from '../../core/api/account.service';
import { MapState } from '../../core/map/map-state';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import { AuthService } from '../../core/auth/auth.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Account', () => {
  function setup(
    overrides: Partial<Record<keyof AccountService, unknown>> = {},
    polarParam?: string,
    isAdmin = true,
  ) {
    const accountService = {
      getPolarStatus: vi
        .fn()
        .mockReturnValue(of({ linked: true, connectedAt: '2026-07-17T10:00:00Z', lastSyncAt: '2026-07-17T11:30:00Z' })),
      getPolarAuthorizeUrl: vi.fn().mockReturnValue(of({ authorizeUrl: 'https://flow.polar.com/x' })),
      sync: vi.fn().mockReturnValue(of({ imported: 3, skipped: 1, failed: 0 })),
      importRides: vi.fn().mockReturnValue(of({ files: [], imported: 2, skipped: 0, failed: 0 })),
      reprocess: vi.fn().mockReturnValue(of({ processed: 5, failed: 0 })),
      deleteAllRides: vi.fn().mockReturnValue(of({ deleted: 7 })),
      closeAccount: vi.fn().mockReturnValue(of(void 0)),
      getSettings: vi.fn().mockReturnValue(of({ maxHeartRate: 190 })),
      updateSettings: vi.fn().mockReturnValue(of(void 0)),
      ...overrides,
    };
    const navigator = { navigate: vi.fn() };
    const auth = { isAdmin: signal(isAdmin), isLoggedIn: signal(true), logout: vi.fn() };
    const router = { navigateByUrl: vi.fn() };
    const mapState = { invalidate: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Account, translocoTesting()],
      providers: [
        { provide: AccountService, useValue: accountService },
        { provide: MapState, useValue: mapState },
        { provide: ExternalNavigator, useValue: navigator },
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(polarParam ? { polar: polarParam } : {}) },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Account);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, accountService, navigator, mapState, auth, router };
  }

  /**
   * The bulk import is the one thing here that crosses riders: raw files live in the database and
   * every rider's history competes for the same 32 GB. It stays closed for that reason, not because
   * of ownership — everything else on this page can only ever reach the caller's own log.
   */
  /**
   * The way out. Distinct from deleting rides, which leaves the Polar link delivering — this takes
   * the rides, the link and the login together, so it asks twice like the other unrecoverable one.
   */
  it('closes the account after both confirmations', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { el, accountService, auth } = setup();

    (el.querySelector('[data-close-account]') as HTMLButtonElement).click();

    expect(confirm).toHaveBeenCalledTimes(2);
    expect(accountService.closeAccount).toHaveBeenCalled();
    // Nothing is left to be signed in as, so the session goes with it.
    expect(auth.logout).toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('leaves the account alone when the rider backs out', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { el, accountService } = setup();

    (el.querySelector('[data-close-account]') as HTMLButtonElement).click();

    expect(accountService.closeAccount).not.toHaveBeenCalled();
    confirm.mockRestore();
  });

  /**
   * The API refuses when this rider is the configured public log. A generic "something went wrong"
   * would send the owner looking for a bug instead of at the setting.
   */
  it('says why closing was refused when this account is the public log', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { fixture, el } = setup({
      closeAccount: vi.fn().mockReturnValue(throwError(() => ({ status: 409 }))),
    });

    (el.querySelector('[data-close-account]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.textContent).toContain('public log');
    confirm.mockRestore();
  });

  it('offers the bulk import to an admin', () => {
    expect(setup({}, undefined, true).el.querySelector('[data-import]')).not.toBeNull();
  });

  it('does not offer the bulk import to an ordinary rider', () => {
    expect(setup({}, undefined, false).el.querySelector('[data-import]')).toBeNull();
  });

  /**
   * Deleting rides is not leaving: the Polar link survives and the next sync starts refilling. A
   * rider who deleted in order to go would otherwise find their rides back the next morning.
   */
  it('warns that deleting rides leaves the Polar link delivering', () => {
    const { el } = setup();

    expect(el.querySelector('.danger-hint')?.textContent).toContain('Polar link stays');
  });

  // The Rides coverage map and the latest-ride background are cached for the session, so every
  // operation that adds, rebuilds or removes rides has to drop those caches.
  it('drops the cached background maps after a sync', () => {
    const { el, mapState } = setup();

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();

    expect(mapState.invalidate).toHaveBeenCalled();
  });

  it('drops the cached background maps after a reprocess', () => {
    const { el, mapState } = setup();

    (el.querySelector('[data-reprocess]') as HTMLButtonElement).click();

    expect(mapState.invalidate).toHaveBeenCalled();
  });

  it('drops the cached background maps after deleting every ride', () => {
    const { el, mapState } = setup();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);

    (el.querySelector('[data-delete-all]') as HTMLButtonElement).click();

    expect(mapState.invalidate).toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('shows the configured max heart rate and saves an update', () => {
    const { el, accountService } = setup();

    const input = el.querySelector('[data-max-hr]') as HTMLInputElement;
    expect(input.value).toBe('190');

    input.value = '185';
    input.dispatchEvent(new Event('input'));
    (el.querySelector('[data-save-settings]') as HTMLButtonElement).click();

    expect(accountService.updateSettings).toHaveBeenCalledWith({ maxHeartRate: 185 });
  });

  it('accepts .fit uploads alongside .gpx and .tcx', () => {
    const { el } = setup();

    const accept = (el.querySelector('input[type="file"]') as HTMLInputElement).accept;
    expect(accept).toContain('.fit');
    expect(accept).toContain('.gpx');
    expect(accept).toContain('.tcx');
  });

  it('starts the Polar connect flow and navigates to the returned url', () => {
    const { el, accountService, navigator } = setup();

    (el.querySelector('[data-connect]') as HTMLButtonElement).click();

    expect(accountService.getPolarAuthorizeUrl).toHaveBeenCalled();
    expect(navigator.navigate).toHaveBeenCalledWith('https://flow.polar.com/x');
  });

  it('shows the last (automatic) sync result from the status', () => {
    const { el } = setup({
      getPolarStatus: vi.fn().mockReturnValue(
        of({
          linked: true,
          connectedAt: '2026-07-17T10:00:00Z',
          lastSyncAt: '2026-07-17T11:30:00Z',
          lastSyncResult: { imported: 2, skipped: 1, failed: 3 },
        }),
      ),
    });

    const text = el.querySelector('[data-last-sync-result]')?.textContent ?? '';
    expect(text).toContain('2'); // imported
    expect(text).toContain('1'); // skipped
    expect(text).toContain('3'); // failed
  });

  it('triggers a sync and shows the summary', () => {
    const { fixture, el, accountService } = setup();

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(accountService.sync).toHaveBeenCalled();
    expect(el.textContent).toContain('3');
  });

  it('shows a success note when returning from a successful Polar link', () => {
    const { el } = setup({}, 'linked');

    expect(el.textContent).toContain('linked');
  });

  it('shows an error when returning from a failed Polar link', () => {
    const { el } = setup({}, 'error');

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('went wrong');
  });

  it('shows the Polar connection state and last sync time', () => {
    const { el } = setup();

    expect(el.textContent).toContain('Connected');
    expect(el.querySelector('[data-last-sync]')?.textContent).toBeTruthy();
  });

  it('shows not connected when Polar is not linked', () => {
    const { el } = setup({ getPolarStatus: vi.fn().mockReturnValue(of({ linked: false })) });

    expect(el.textContent).toContain('Not connected');
  });

  it('refreshes the status after a sync', () => {
    const { fixture, el, accountService } = setup();
    expect(accountService.getPolarStatus).toHaveBeenCalledTimes(1);

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(accountService.getPolarStatus).toHaveBeenCalledTimes(2);
  });

  it('shows an error message when the sync fails', () => {
    const { fixture, el } = setup({ sync: vi.fn().mockReturnValue(throwError(() => new Error('boom'))) });

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('went wrong');
  });

  it('imports selected files and shows the result', () => {
    const { fixture, el, accountService } = setup();
    const component = fixture.componentInstance;

    const file = new File(['<gpx/>'], 'ride.gpx');
    component.onFilesSelected({ target: { files: [file] } } as unknown as Event);
    fixture.detectChanges();
    (el.querySelector('[data-import]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(accountService.importRides).toHaveBeenCalledWith([file]);
    expect(el.textContent).toContain('2');
  });

  it('reprocesses stored rides and shows the counts', () => {
    const { fixture, el, accountService } = setup();

    (el.querySelector('[data-reprocess]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(accountService.reprocess).toHaveBeenCalled();
    expect(el.textContent).toContain('5'); // processed count
  });

  it('deletes all rides after a double confirmation', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { fixture, el, accountService } = setup();

    (el.querySelector('[data-delete-all]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(confirm).toHaveBeenCalledTimes(2); // double confirmation for a destructive action
    expect(accountService.deleteAllRides).toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('does not delete when the confirmation is declined', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { el, accountService } = setup();

    (el.querySelector('[data-delete-all]') as HTMLButtonElement).click();

    expect(accountService.deleteAllRides).not.toHaveBeenCalled();
    confirm.mockRestore();
  });
});
