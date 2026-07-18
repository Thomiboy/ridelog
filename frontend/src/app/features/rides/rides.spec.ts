import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Rides } from './rides';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import type { Paged, RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Rides', () => {
  function setup(paged: Paged<RideSummary>, loggedIn = false) {
    const ridesService = {
      getRides: vi.fn().mockReturnValue(of(paged)),
      deleteRide: vi.fn().mockReturnValue(of(void 0)),
    };
    const mapState = { reset: vi.fn(), invalidate: vi.fn() };
    const authService = { isLoggedIn: signal(loggedIn) };
    TestBed.configureTestingModule({
      imports: [Rides, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
        { provide: AuthService, useValue: authService },
      ],
    });
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(Rides);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState, authService, router };
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

  it('restores the latest-ride background map on entry', () => {
    const { mapState } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    expect(mapState.reset).toHaveBeenCalled();
  });

  it('navigates to the ride when its row is clicked', () => {
    const { el, router } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });
    const navigate = vi.spyOn(router, 'navigateByUrl');

    (el.querySelector('[data-ride]') as HTMLTableRowElement).click();

    expect(navigate).toHaveBeenCalledWith('/rides/r1');
  });

  it('hides the delete button from anonymous visitors', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 }, false);

    expect(el.querySelector('[data-delete]')).toBeNull();
  });

  it('shows a delete button per row when logged in', () => {
    const { el } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 20, total: 2 }, true);

    expect(el.querySelectorAll('[data-delete]').length).toBe(2);
  });

  it('deletes a ride after confirmation, refreshes the list, and does not navigate', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const { el, ridesService, mapState, router } = setup(
      { items: [ride('r1')], page: 1, pageSize: 20, total: 1 },
      true,
    );
    const navigate = vi.spyOn(router, 'navigateByUrl');

    (el.querySelector('[data-delete]') as HTMLButtonElement).click();

    expect(ridesService.deleteRide).toHaveBeenCalledWith('r1');
    expect(mapState.invalidate).toHaveBeenCalled();
    expect(ridesService.getRides).toHaveBeenCalledTimes(2); // initial load + refresh
    expect(navigate).not.toHaveBeenCalled(); // the row click was suppressed
    confirm.mockRestore();
  });

  it('does not delete when the confirmation is declined', () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    const { el, ridesService } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 }, true);

    (el.querySelector('[data-delete]') as HTMLButtonElement).click();

    expect(ridesService.deleteRide).not.toHaveBeenCalled();
    confirm.mockRestore();
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
