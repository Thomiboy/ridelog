import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { MonthlyAnalysis } from './monthly-analysis';
import { AnalysisService } from '../../core/api/analysis.service';
import { LanguageService } from '../../core/i18n/language.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import type { MonthlyAnalysisResult } from '../../core/api/analysis.models';

/**
 * The Monthly analysis section (#187). Generated prose cannot pass through Transloco — it is written
 * again or it is not — so the language a reading was written in is part of what identifies it, and
 * the page has to be honest about showing one written in the other language.
 */
describe('MonthlyAnalysis', () => {
  const stored: MonthlyAnalysisResult = {
    id: 'a-1',
    year: 2026,
    month: 6,
    language: 'Hungarian',
    text: 'Júniusban 240 kilométert tekertél.',
    model: 'claude-opus-5',
    rideCount: 6,
    writtenAt: '2026-07-01T10:00:00Z',
  };

  function setup(options: {
    read?: ReturnType<AnalysisService['read']>;
    write?: ReturnType<AnalysisService['write']>;
    language?: 'en' | 'hu';
  } = {}) {
    const analysis = {
      read: vi.fn().mockReturnValue(options.read ?? throwError(() => ({ status: 404 }))),
      write: vi.fn().mockReturnValue(options.write ?? of(stored)),
      remove: vi.fn().mockReturnValue(of(void 0)),
    };
    TestBed.configureTestingModule({
      imports: [MonthlyAnalysis, translocoTesting()],
      providers: [
        { provide: AnalysisService, useValue: analysis },
        { provide: LanguageService, useValue: { current: () => options.language ?? 'hu' } },
      ],
    });
    const fixture = TestBed.createComponent(MonthlyAnalysis);
    fixture.componentRef.setInput('months', [
      { year: 2026, month: 6, distanceKm: 240, elevationGainMeters: 900, rideCount: 6, calories: 5000, durationMinutes: 700 },
      { year: 2026, month: 5, distanceKm: 180, elevationGainMeters: 600, rideCount: 4, calories: 3800, durationMinutes: 520 },
    ]);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, analysis };
  }

  it('shows the stored reading for the selected month', () => {
    const { fixture } = setup({ read: of(stored) });

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Júniusban 240 kilométert tekertél.');
  });

  /**
   * The provenance note, in the same habit as the ride page's temperature and weather notes: this
   * project says where a number came from, and this says where the sentences came from.
   */
  it('says where the sentences came from', () => {
    const { fixture } = setup({ read: of(stored) });

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-note]')).not.toBeNull();
  });

  /** A month nobody has asked about yet offers the button, and has asked nothing of the model. */
  it('offers to write one for a month that has none', () => {
    const { fixture, analysis } = setup();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-generate]')).not.toBeNull();
    expect(analysis.write).not.toHaveBeenCalled();
  });

  it('writes one when asked, and only when asked', () => {
    const { component, analysis } = setup();

    component.generate();

    expect(analysis.write).toHaveBeenCalledWith(2026, 6, 'Hungarian');
  });

  /**
   * The heart of the language rule: a reading written in Hungarian stays readable under an English
   * UI, with a note saying so — switching language must never quietly spend another generation.
   */
  it('shows a reading written in the other language rather than writing a new one', () => {
    const { fixture, component, analysis } = setup({ language: 'en', read: of(stored) });

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Júniusban 240 kilométert tekertél.');
    expect(component.inOtherLanguage()).toBe(true);
    expect(analysis.write).not.toHaveBeenCalled();
  });

  it('names the refusal when a closed month already has one', () => {
    const { fixture, component } = setup({
      write: throwError(() => ({ status: 409, error: { refusal: 'AlreadyWritten' } })),
    });

    component.generate();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('already');
  });

  it('lets a reading go so the month can be written again', () => {
    const { component, analysis } = setup({ read: of(stored) });

    component.remove();

    expect(analysis.remove).toHaveBeenCalledWith('a-1');
  });
});
