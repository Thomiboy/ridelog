import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Rides } from './rides';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { RidesViewState, type RidesView } from './rides-view-state';
import { AuthService } from '../../core/auth/auth.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import type { SnapState } from '../../layout/bottom-sheet/snap';
import type { Paged, RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Rides', () => {
  // A ride on a given day of the *current* month, so it lands in the calendar's default month.
  const calRide = (id: string, distanceKm: number, day = 15): RideSummary => {
    const now = new Date();
    return {
      ...ride(id),
      startTime: new Date(now.getFullYear(), now.getMonth(), day, 8, 0, 0).toISOString(),
      distanceKm,
    };
  };

  function setup(
    paged: Paged<RideSummary>,
    loggedIn = false,
    queryParams: Record<string, string> = {},
    snap: SnapState = 'half',
    calendarRides: RideSummary[] = [calRide('c1', 42)],
    // The real default is 'calendar'; most tests exercise the list, so default the setup to 'list'.
    startView: RidesView = 'list',
  ) {
    const ridesService = {
      getRides: vi.fn().mockReturnValue(of(paged)),
      deleteRide: vi.fn().mockReturnValue(of(void 0)),
      getAllRides: vi.fn().mockReturnValue(of(calendarRides)),
    };
    const mapState = { reset: vi.fn(), invalidate: vi.fn(), showAllRoutes: vi.fn() };
    const authService = { isLoggedIn: signal(loggedIn) };
    const sheetState = { current: signal<SnapState>(snap), request: vi.fn() };
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
    const viewState = TestBed.inject(RidesViewState);
    viewState.view.set(startView);
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(Rides);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridesService, mapState, authService, sheetState, viewState, router };
  }

  function showCalendar(ctx: ReturnType<typeof setup>) {
    (ctx.el.querySelector('[data-view="calendar"]') as HTMLButtonElement).click();
    ctx.fixture.detectChanges();
  }

  const ride = (id: string, sources: string[] = ['PolarAutoSync']): RideSummary => ({
    id,
    startTime: '2026-06-01T08:00:00Z',
    distanceKm: 61.5,
    durationMinutes: 118,
    sport: 'ROAD_BIKING',
    averageSpeedKmh: 31.3,
    elevationGainMeters: 460,
    sources,
  });

  it('renders a row per ride', () => {
    const { el } = setup({ items: [ride('r1'), ride('r2')], page: 1, pageSize: 20, total: 2 });

    expect(el.querySelectorAll('[data-ride]').length).toBe(2);
    expect(el.textContent).toContain('61.5');
  });

  it('shows a source chip per token in the row', () => {
    const { el } = setup({ items: [ride('r1', ['PolarAutoSync', 'Bryton'])], page: 1, pageSize: 20, total: 1 });

    const chips = [...el.querySelectorAll('[data-ride] [data-source-chip]')].map((c) => c.textContent?.trim());
    expect(chips).toEqual(['Polar · Auto-sync', 'Bryton']);
  });

  it('shows the duration as hours and minutes', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    expect(el.textContent).toContain('1h 58m'); // 118 minutes
    expect(el.textContent).not.toContain('118 min');
  });

  it('opens the ride from the row, without a separate date link', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    // The whole row navigates now, so the date is plain text — no anchor.
    expect(el.querySelector('a[href="/rides/r1"]')).toBeNull();
    expect(el.querySelector('[data-ride]')?.textContent).toContain('Jun 1, 2026');
  });

  it('paints every route on the background map on entry', () => {
    const { mapState } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    // The Rides page is the "where have I been" overview, so its backdrop is every route at once,
    // not the latest ride.
    expect(mapState.showAllRoutes).toHaveBeenCalled();
    expect(mapState.reset).not.toHaveBeenCalled();
  });

  it('restores the default background when leaving the page', () => {
    const { fixture, mapState } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    fixture.destroy();

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

  it('renders the list view', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    expect(el.querySelector('table.rides')).not.toBeNull();
  });

  it('offers only the list and calendar views — routes live on the background map', () => {
    const { el } = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    expect([...el.querySelectorAll('[data-view]')].map((b) => b.getAttribute('data-view'))).toEqual([
      'list',
      'calendar',
    ]);
    expect(el.querySelector('app-route-map')).toBeNull(); // no map embedded in the page
  });

  it('defaults to the calendar view', () => {
    // Fresh RidesViewState (no override) → the calendar is the default view.
    const ctx = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 }, false, {}, 'half', [calRide('c1', 42)], 'calendar');

    expect(ctx.el.querySelector('[data-cal-day]')).not.toBeNull();
    expect(ctx.el.querySelector('table.rides')).toBeNull();
    expect(ctx.ridesService.getAllRides).toHaveBeenCalled();
  });

  it('restores the view you left from when returning to the page', () => {
    const paged = { items: [ride('r1')], page: 1, pageSize: 20, total: 1 };
    const ctx = setup(paged); // starts on the list

    // Switch to the calendar, then leave and come back (a new component, same shared view state).
    showCalendar(ctx);
    ctx.fixture.destroy();
    const returned = TestBed.createComponent(Rides);
    returned.detectChanges();

    const el = returned.nativeElement as HTMLElement;
    expect(el.querySelector('[data-cal-day]')).not.toBeNull();
    expect(el.querySelector('table.rides')).toBeNull();
  });

  it('switches to a calendar grid built from all rides', () => {
    const ctx = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });

    showCalendar(ctx);

    expect(ctx.ridesService.getAllRides).toHaveBeenCalled();
    expect(ctx.el.querySelectorAll('[data-cal-day]').length).toBeGreaterThanOrEqual(28);
    // The seeded ride's day shows its distance.
    const dayCell = ctx.el.querySelector('[data-cal-day].has-rides');
    expect(dayCell?.textContent).toContain('42');
    expect(ctx.el.querySelector('table.rides')).toBeNull();
  });

  it('navigates between months', () => {
    const ctx = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });
    showCalendar(ctx);

    const label = ctx.el.querySelector('[data-cal-label]')?.textContent;
    (ctx.el.querySelector('[data-cal-prev]') as HTMLButtonElement).click();
    ctx.fixture.detectChanges();

    expect(ctx.el.querySelector('[data-cal-label]')?.textContent).not.toBe(label);
    // Last month has no rides, so the seeded day is gone.
    expect(ctx.el.querySelector('[data-cal-day].has-rides')).toBeNull();
  });

  it('opens the ride detail when a single-ride day is clicked', () => {
    const ctx = setup({ items: [ride('r1')], page: 1, pageSize: 20, total: 1 });
    const navigate = vi.spyOn(ctx.router, 'navigateByUrl');
    showCalendar(ctx);

    (ctx.el.querySelector('[data-cal-day].has-rides') as HTMLElement).click();

    expect(navigate).toHaveBeenCalledWith('/rides/c1');
  });

  it("lists the day's rides in a panel when a multi-ride day is clicked", () => {
    const ctx = setup(
      { items: [ride('r1')], page: 1, pageSize: 20, total: 1 },
      false,
      {},
      'half',
      [calRide('c1', 30), calRide('c2', 20)],
    );
    showCalendar(ctx);

    (ctx.el.querySelector('[data-cal-day].has-rides') as HTMLElement).click();
    ctx.fixture.detectChanges();

    const panel = ctx.el.querySelector('[data-cal-day-rides]');
    expect(panel).not.toBeNull();
    expect(panel!.querySelectorAll('a').length).toBe(2);
  });
});
