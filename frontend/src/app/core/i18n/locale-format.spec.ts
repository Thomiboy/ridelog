import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { translocoLocaleProviders } from './transloco-locale-providers';

@Component({
  standalone: true,
  imports: [TranslocoDatePipe, TranslocoDecimalPipe],
  template: `
    <span data-num>{{ 1234.5 | translocoDecimal: { minimumFractionDigits: 1, maximumFractionDigits: 1 } }}</span>
    <span data-date>{{ date | translocoDate: { dateStyle: 'medium' } }}</span>
  `,
})
class Host {
  readonly date = new Date(2026, 5, 1); // 1 June 2026, local
}

function setup() {
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: { en: {}, hu: {} },
        translocoConfig: { availableLangs: ['en', 'hu'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [translocoLocaleProviders],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    transloco: TestBed.inject(TranslocoService),
    num: () => el.querySelector('[data-num]')?.textContent ?? '',
    date: () => el.querySelector('[data-date]')?.textContent ?? '',
  };
}

describe('locale formatting follows the active language', () => {
  it('renders English (en-US) numbers and dates by default', () => {
    const ctx = setup();
    expect(ctx.num()).toContain('234.5'); // dot decimal
    expect(ctx.date()).toContain('Jun 1, 2026'); // month-day-year
  });

  it('renders Hungarian (hu-HU) numbers and dates when the language is hu', () => {
    const ctx = setup();
    ctx.transloco.setActiveLang('hu');
    ctx.fixture.detectChanges();
    expect(ctx.num()).toContain('234,5'); // comma decimal
    expect(ctx.date()).toContain('2026.'); // year-first
    expect(ctx.date()).toContain('jún'); // Hungarian June
  });
});
