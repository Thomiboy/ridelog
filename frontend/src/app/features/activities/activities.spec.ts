import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Activities } from './activities';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Activities', () => {
  function setup(items: unknown[]) {
    const ridesService = {
      getOtherActivities: vi.fn().mockReturnValue(of({ items, page: 1, pageSize: 100, total: items.length })),
    };
    TestBed.configureTestingModule({
      imports: [Activities, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: { reset: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(Activities);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  // The list is only half a feature until you can open one. Detail lives at /rides/:id for both
  // kinds — the page reads which list it belongs to off the recording itself.
  it('opens an activity on the detail page', () => {
    const el = setup([
      {
        id: 'a1',
        startTime: '2026-06-01T08:00:00Z',
        distanceKm: 9,
        durationMinutes: 55,
        sport: 'RUNNING',
        sportCategory: 'Running',
        sources: [],
      },
    ]);

    expect(el.querySelector('[data-activity]')!.getAttribute('href')).toBe('/rides/a1');
  });

  it('says so when nothing but rides has been recorded', () => {
    const el = setup([]);

    expect(el.textContent).toContain('Nothing but rides so far.');
  });
});
