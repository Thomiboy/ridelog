import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Admin } from './admin';
import { AdminService } from '../../core/api/admin.service';
import { ExternalNavigator } from '../../core/navigation/external-navigator';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Admin', () => {
  function setup(overrides: Partial<Record<keyof AdminService, unknown>> = {}) {
    const adminService = {
      getPolarAuthorizeUrl: vi.fn().mockReturnValue(of({ authorizeUrl: 'https://flow.polar.com/x' })),
      sync: vi.fn().mockReturnValue(of({ imported: 3, skipped: 1, failed: 0 })),
      importRides: vi.fn().mockReturnValue(of({ files: [], imported: 2, skipped: 0, failed: 0 })),
      ...overrides,
    };
    const navigator = { navigate: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Admin, translocoTesting()],
      providers: [
        { provide: AdminService, useValue: adminService },
        { provide: ExternalNavigator, useValue: navigator },
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

  it('triggers a sync and shows the summary', () => {
    const { fixture, el, adminService } = setup();

    (el.querySelector('[data-sync]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(adminService.sync).toHaveBeenCalled();
    expect(el.textContent).toContain('3');
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
});
