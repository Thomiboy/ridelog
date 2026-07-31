import { TestBed } from '@angular/core/testing';
import { afterEach, vi } from 'vitest';
import { ThemeService } from './theme.service';

function stubMatchMedia(dark: boolean) {
  const listeners: ((e: { matches: boolean }) => void)[] = [];
  const mql = {
    matches: dark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: (_: string, cb: (e: { matches: boolean }) => void) => listeners.push(cb),
    removeEventListener: vi.fn(),
    // The deprecated pair as well: Angular CDK's BreakpointObserver still calls addListener, and a
    // stub that only answers the modern half throws the moment anything using it is rendered.
    addListener: (cb: (e: { matches: boolean }) => void) => listeners.push(cb),
    removeListener: vi.fn(),
  };
  vi.stubGlobal('matchMedia', vi.fn(() => mql));
  return {
    fireOsChange(nowDark: boolean) {
      mql.matches = nowDark;
      listeners.forEach((cb) => cb({ matches: nowDark }));
    },
  };
}

// Without this the stubbed matchMedia outlives this file and is inherited by whatever the worker
// runs next, where it belongs to nothing and matches no real browser.
afterEach(() => vi.unstubAllGlobals());

function create() {
  TestBed.configureTestingModule({});
  return TestBed.inject(ThemeService);
}

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  it('defaults to following the system, resolving via the OS preference', () => {
    stubMatchMedia(true); // OS is in dark mode
    const service = create();

    expect(service.preference()).toBe('system');
    expect(service.resolved()).toBe('dark');
    // System mode lets the browser pick per OS.
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('forces a chosen theme, remembers it, and sets the root color-scheme', () => {
    stubMatchMedia(false);
    const service = create();

    service.use('dark');

    expect(service.preference()).toBe('dark');
    expect(service.resolved()).toBe('dark');
    expect(localStorage.getItem('ridelog.theme')).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('follows OS changes live while in system mode', () => {
    const os = stubMatchMedia(false);
    const service = create();
    expect(service.resolved()).toBe('light');

    os.fireOsChange(true);

    expect(service.resolved()).toBe('dark');
  });

  it('ignores OS changes once a fixed theme is chosen', () => {
    const os = stubMatchMedia(false);
    const service = create();
    service.use('light');

    os.fireOsChange(true);

    expect(service.resolved()).toBe('light');
  });

  it('restores the saved preference on startup', () => {
    localStorage.setItem('ridelog.theme', 'dark');
    stubMatchMedia(false);

    expect(create().preference()).toBe('dark');
  });
});
