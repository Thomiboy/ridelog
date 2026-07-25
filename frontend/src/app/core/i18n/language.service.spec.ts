import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TranslocoService } from '@jsverse/transloco';
import { LanguageService } from './language.service';

function makeTransloco() {
  return {
    getDefaultLang: () => 'en',
    setActiveLang: vi.fn(),
    load: vi.fn().mockReturnValue(of({})),
  };
}

function create(transloco = makeTransloco()) {
  TestBed.configureTestingModule({
    providers: [{ provide: TranslocoService, useValue: transloco }],
  });
  return { service: TestBed.inject(LanguageService), transloco };
}

describe('LanguageService', () => {
  beforeEach(() => localStorage.clear());

  it('defaults to the transloco default language', () => {
    const { service } = create();
    expect(service.current()).toBe('en');
  });

  it('switches, remembers and activates the chosen language', () => {
    const { service, transloco } = create();

    service.use('hu');

    expect(service.current()).toBe('hu');
    expect(transloco.setActiveLang).toHaveBeenCalledWith('hu');
    expect(localStorage.getItem('ridelog.lang')).toBe('hu');
  });

  it('restores the saved language on startup', () => {
    localStorage.setItem('ridelog.lang', 'hu');

    const { service } = create();

    expect(service.current()).toBe('hu');
  });

  it('ignores an unknown saved language', () => {
    localStorage.setItem('ridelog.lang', 'de');

    const { service } = create();

    expect(service.current()).toBe('en');
  });

  it('activates and loads the current language on init', () => {
    localStorage.setItem('ridelog.lang', 'hu');
    const { service, transloco } = create();

    service.init();

    expect(transloco.setActiveLang).toHaveBeenCalledWith('hu');
    expect(transloco.load).toHaveBeenCalledWith('hu');
  });
});
