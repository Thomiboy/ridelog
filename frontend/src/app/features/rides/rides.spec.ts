import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Rides } from './rides';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import type { SnapState } from '../../layout/bottom-sheet/snap';
import type { Paged, RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Rides', () => {
  function setup(
    paged: Paged<RideSummary>,
    loggedIn = false,
    queryParams: Record<string, string> = {},
    snap: SnapState = 'half',
  ) {
    const ridesService = {
      getRides: vi.fn().mockReturnValue(of(paged)),
      deleteRide: vi.fn().mockReturnValue(of(void 0)),
    };
    const mapState = { reset: vi.fn(), invalidate: vi.fn() };
    const authService = { isLoggedIn: signal(loggedIn) };
    const sheetState = { current: signal<SnapState>(snap) };
    const route = { snapshot: { queryParamMap: convertToParamMap(queryParams) } };
    TestBed.configureTestingModule({
      imports: [Rides, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: RidesService, useValue: ridesService },
        { provide: MapState, useValue: mapState },
        { provide: AuthService, useValue: authService },
        { provide: SheetState, useValue: sheetState },
        { provide: ActivatedRoute, useValue: route },
      ],
    });
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(Rides);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState, authService, sheetState, router };
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

  it('requests a full sheet page size of 18 rides', () => {
    const { ridesService } = setup({ items: [], page: 1, pageSize: 18, total: 0 }, false, {}, 'full');

    expect(ridesService.getRides).toHaveBeenCalledWith(1, 18);
  });

  it('requests a half sheet page size of 8 rides', () => {
    const { ridesService } = setup({ items: [], page: 1, pageSize: 8, total: 0 }, false, {}, 'half');

    expect(ridesService.getRides).toHaveBeenCalledWith(1, 8);
  });

  it('reloads with the new page size when the sheet snap changes', () => {
    const { fixture, ridesService, sheetState } = setup({ items: [], page: 1, pageSize: 9, total: 0 }, false, {}, 'half');

    sheetState.current.set('full');
    fixture.detectChanges();

    expect(ridesService.getRides).toHaveBeenCalledWith(1, 18);
  });

  it('loads the page from the ?page query param on entry', () => {
    const { ridesService } = setup({ items: [ride('r1')], page: 2, pageSize: 20, total: 40 }, false, { page: '2' });

    expect(ridesService.getRides.mock.calls[0][0]).toBe(2);
  });

  it('shows the current page and total page count', () => {
    const { el } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 2, total: 3 });

    // 3 rides at 2 per page → 2 pages.
    expect(el.querySelector('[data-page-indicator]')?.textContent).toContain('1 / 2');
  });

  it('reflects the page in the URL when paging', () => {
    const { el, router } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 2, total: 3 });
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    (el.querySelector('[data-next]') as HTMLButtonElement).click();

    expect(navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { page: 2 } }));
  });

  it('loads the next page when there is one', () => {
    const { el, ridesService } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 2, total: 3 });

    const prev = el.querySelector('[data-prev]') as HTMLButtonElement;
    const next = el.querySelector('[data-next]') as HTMLButtonElement;
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);

    next.click();
    expect(ridesService.getRides).toHaveBeenCalledWith(2, 8); // half sheet → 8 per page
  });

  it('has no next page on the last page', () => {
    const { el } = setup({ items: [ride('r1')], page: 2, pageSize: 2, total: 3 });

    expect((el.querySelector('[data-next]') as HTMLButtonElement).disabled).toBe(true);
    expect((el.querySelector('[data-prev]') as HTMLButtonElement).disabled).toBe(false);
  });
});
