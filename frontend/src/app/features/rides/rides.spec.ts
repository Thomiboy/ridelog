import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Rides } from './rides';
import { RidesService } from '../../core/api/rides.service';
import type { Paged, RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Rides', () => {
  function setup(paged: Paged<RideSummary>) {
    const ridesService = { getRides: vi.fn().mockReturnValue(of(paged)) };
    TestBed.configureTestingModule({
      imports: [Rides, translocoTesting()],
      providers: [provideRouter([]), { provide: RidesService, useValue: ridesService }],
    });
    const fixture = TestBed.createComponent(Rides);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService };
  }

  const ride = (id: string): RideSummary => ({
    id,
    startTime: '2026-06-01T08:00:00Z',
    distanceKm: 61.5,
    durationMinutes: 118,
    sport: 'ROAD_BIKING',
    averageSpeedKmh: 31.3,
    elevationGainMeters: 460,
  });

  it('renders a row per ride', () => {
    const { el } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 20, total: 2 });

    expect(el.querySelectorAll('[data-ride]').length).toBe(2);
    expect(el.textContent).toContain('61.5');
  });

  it('links each ride to its detail page', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    expect(el.querySelector('a[href="/rides/r1"]')).toBeTruthy();
  });

  it('shows an empty state when there are no rides', () => {
    const { el } = setup({ items: [], page: 1, pageSize: 20, total: 0 });

    expect(el.querySelectorAll('[data-ride]').length).toBe(0);
    expect(el.querySelector('.empty')?.textContent).toContain('No rides yet');
  });

  it('loads the next page when there is one', () => {
    const { el, ridesService } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 2, total: 3 });

    const prev = el.querySelector('[data-prev]') as HTMLButtonElement;
    const next = el.querySelector('[data-next]') as HTMLButtonElement;
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);

    next.click();
    expect(ridesService.getRides).toHaveBeenCalledWith(2);
  });

  it('has no next page on the last page', () => {
    const { el } = setup({ items: [ride('r1')], page: 2, pageSize: 2, total: 3 });

    expect((el.querySelector('[data-next]') as HTMLButtonElement).disabled).toBe(true);
    expect((el.querySelector('[data-prev]') as HTMLButtonElement).disabled).toBe(false);
  });
});
