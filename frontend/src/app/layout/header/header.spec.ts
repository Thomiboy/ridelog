import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { Header } from './header';
import { AuthService } from '../../core/auth/auth.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Header', () => {
  function setup(state: { loggedIn: boolean; admin: boolean }) {
    const auth = {
      isLoggedIn: signal(state.loggedIn),
      isAdmin: signal(state.admin),
      logout: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [Header, translocoTesting()],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(Header);
    fixture.detectChanges();
    return { fixture, auth, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
  }

  it('shows a login link when logged out', () => {
    const { text } = setup({ loggedIn: false, admin: false });
    expect(text()).toContain('Log in');
    expect(text()).not.toContain('Log out');
  });

  it('always links to Statistics', () => {
    const { fixture, text } = setup({ loggedIn: false, admin: false });
    expect(text()).toContain('Statistics');
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[href="/statistics"]');
    expect(link).not.toBeNull();
  });

  it('shows logout and the admin link for a logged-in admin', () => {
    const { text } = setup({ loggedIn: true, admin: true });
    expect(text()).toContain('Log out');
    expect(text()).toContain('Admin');
  });

  it('hides the admin link for non-admins', () => {
    const { text } = setup({ loggedIn: true, admin: false });
    expect(text()).not.toContain('Admin');
  });

  it('logs out when the logout button is clicked', () => {
    const { fixture, auth } = setup({ loggedIn: true, admin: false });
    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    expect(auth.logout).toHaveBeenCalled();
  });
});
