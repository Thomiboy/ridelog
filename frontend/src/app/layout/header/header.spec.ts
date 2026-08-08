import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { Header } from './header';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { ThemeService } from '../../core/theme/theme.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Header', () => {
  function setup(state: { loggedIn: boolean; admin: boolean }) {
    const auth = {
      isLoggedIn: signal(state.loggedIn),
      isAdmin: signal(state.admin),
      logout: vi.fn(),
    };
    const language = { current: signal('en'), use: vi.fn() };
    const theme = { preference: signal('system'), use: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Header, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: LanguageService, useValue: language },
        { provide: ThemeService, useValue: theme },
      ],
    });
    const fixture = TestBed.createComponent(Header);
    fixture.detectChanges();
    return { fixture, auth, language, theme, el: fixture.nativeElement as HTMLElement, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
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

  it('shows logout and the account link for a logged-in admin', () => {
    const { text } = setup({ loggedIn: true, admin: true });
    expect(text()).toContain('Log out');
    expect(text()).toContain('Account');
  });

  /**
   * The page behind this link is a rider's own — their Polar link, their zones, their rides. Only
   * the bulk import on it crosses riders, and that card is what hides, not the link.
   */
  it('shows the link to any signed-in rider', () => {
    const { text } = setup({ loggedIn: true, admin: false });
    expect(text()).toContain('Account');
  });

  it('hides the link from a visitor who is not signed in', () => {
    const { text } = setup({ loggedIn: false, admin: false });
    expect(text()).not.toContain('Account');
  });

  it('logs out when the logout button is clicked', () => {
    const { fixture, auth } = setup({ loggedIn: true, admin: false });
    (fixture.nativeElement as HTMLElement).querySelector('[data-logout]')!.dispatchEvent(new Event('click'));
    expect(auth.logout).toHaveBeenCalled();
  });

  it('switches language when a switcher option is clicked', () => {
    const { el, language } = setup({ loggedIn: false, admin: false });

    (el.querySelector('[data-lang="hu"]') as HTMLButtonElement).click();

    expect(language.use).toHaveBeenCalledWith('hu');
  });

  it('marks the active language', () => {
    const { el } = setup({ loggedIn: false, admin: false });

    expect(el.querySelector('[data-lang="en"]')?.classList.contains('active')).toBe(true);
    expect(el.querySelector('[data-lang="hu"]')?.classList.contains('active')).toBe(false);
  });

  it('switches theme via the three-state switcher, marking the active one', () => {
    const { el, theme } = setup({ loggedIn: false, admin: false });

    expect(el.querySelector('[data-theme="system"]')?.classList.contains('active')).toBe(true);
    (el.querySelector('[data-theme="dark"]') as HTMLButtonElement).click();

    expect(theme.use).toHaveBeenCalledWith('dark');
  });
});
