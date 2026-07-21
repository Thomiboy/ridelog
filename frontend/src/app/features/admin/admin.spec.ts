import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { Admin } from './admin';
import { AdminService } from '../../core/api/admin.service';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Admin', () => {
  function setup(overrides: Partial<Record<keyof AdminService, unknown>> = {}, polarParam?: string) {
    const adminService = {
      getPolarStatus: vi
        .fn()
        .mockReturnValue(of({ linked: true, connectedAt: '2026-07-17T10:00:00Z', lastSyncAt: '2026-07-17T11:30:00Z' })),
      getPolarAuthorizeUrl: vi.fn().mockReturnValue(of({ authorizeUrl: 'https://flow.polar.com/x' })),
      sync: vi.fn().mockReturnValue(of({ imported: 3, skipped: 1, failed: 0 })),
      importRides: vi.fn().mockReturnValue(of({ files: [], imported: 2, skipped: 0, failed: 0 })),
      reprocess: vi.fn().mockReturnValue(of({ processed: 5, failed: 0 })),
      deleteAllRides: vi.fn().mockReturnValue(of({ deleted: 7 })),
      ...overrides,
    };
    const navigator = { navigate: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Admin, translocoTesting()],
      providers: [
        { provide: AdminService, useValue: adminService },
        { provide: ExternalNavigator, useValue: navigator },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(polarParam ? { polar: polarParam } : {}) },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Admin);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, adminService, navigator };
  }

  it('starts the Polar connect flow and navigates to the returned url', () => {
    const { el, adminService, navigator } = setup();

    (el.querySelector('[data-connect]') as HTMLButtonElement).click();

    expect(adminService.getPolarAuthorizeUrl).toHaveBeenCalled();
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
    const { fixture, el, adminService } = setup();

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(adminService.sync).toHaveBeenCalled();
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
    const { fixture, el, adminService } = setup();
    expect(adminService.getPolarStatus).toHaveBeenCalledTimes(1);

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(adminService.getPolarStatus).toHaveBeenCalledTimes(2);
  });

  it('shows an error message when the sync fails', () => {
    const { fixture, el } = setup({ sync: vi.fn().mockReturnValue(throwError(() => new Error('boom'))) });

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('went wrong');
  });

  it('imports selected files and shows the result', () => {
    const { fixture, el, adminService } = setup();
    const component = fixture.componentInstance;

    const file = new File(['<gpx/>'], 'ride.gpx');
    component.onFilesSelected({ target: { files: [file] } } as unknown as Event);
    fixture.detectChanges();
    (el.querySelector('[data-import]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(adminService.importRides).toHaveBeenCalledWith([file]);
    expect(el.textContent).toContain('2');
  });

  it('reprocesses stored rides and shows the counts', () => {
    const { fixture, el, adminService } = setup();

    (el.querySelector('[data-reprocess]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(adminService.reprocess).toHaveBeenCalled();
    expect(el.textContent).toContain('5'); // processed count
  });

  it('deletes all rides after a double confirmation', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { fixture, el, adminService } = setup();

    (el.querySelector('[data-delete-all]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(confirm).toHaveBeenCalledTimes(2); // double confirmation for a destructive action
    expect(adminService.deleteAllRides).toHaveBeenCalled();
    confirm.mockRestore();
  });

  it('does not delete when the confirmation is declined', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { el, adminService } = setup();

    (el.querySelector('[data-delete-all]') as HTMLButtonElement).click();

    expect(adminService.deleteAllRides).not.toHaveBeenCalled();
    confirm.mockRestore();
  });
});
