import { TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import { DurationPipe } from './duration.pipe';

const en = { format: { durationHours: 'h', durationMinutes: 'm' } };
const hu = { format: { durationHours: 'ó', durationMinutes: 'p' } };

function setup() {
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { en, hu },
        translocoConfig: { availableLangs: ['en', 'hu'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [DurationPipe],
  });
  return { pipe: TestBed.inject(DurationPipe), transloco: TestBed.inject(TranslocoService) };
}

describe('DurationPipe', () => {
  it('formats with English units and no spacing by default', () => {
    const { pipe } = setup();
    expect(pipe.transform(118)).toBe('1h 58m');
  });

  it('uses the translated units and Hungarian spacing when the language is hu', () => {
    const { pipe, transloco } = setup();
    transloco.setActiveLang('hu');
    expect(pipe.transform(118)).toBe('1 ó 58 p');
  });
});
