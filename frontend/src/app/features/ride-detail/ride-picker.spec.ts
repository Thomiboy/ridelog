import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RidePicker } from './ride-picker';
import type { RideSummary } from '../../core/api/ride.models';
import { translocoTesting } from '../../core/i18n/transloco-testing';

const ride = (id: string, startTime: string, distanceKm: number): RideSummary => ({
  id,
  startTime,
  distanceKm,
  durationMinutes: 90,
  sport: 'ROAD_BIKING',
  sources: [],
});

// Host so we can bind inputs and capture the pick output.
@Component({
  imports: [RidePicker],
  template: `<app-ride-picker [rides]="rides()" [excludeId]="excludeId()" (pick)="picked = $event" (close)="closed = true" />`,
})
class Host {
  readonly rides = signal<RideSummary[]>([]);
  readonly excludeId = signal('');
  picked: RideSummary | null = null;
  closed = false;
}

describe('RidePicker', () => {
  function setup(rides: RideSummary[], excludeId = '') {
    TestBed.configureTestingModule({ imports: [Host, translocoTesting()] });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.rides.set(rides);
    fixture.componentInstance.excludeId.set(excludeId);
    fixture.detectChanges();
    return { fixture, host: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  const rides = [
    ride('r1', '2026-06-01T08:00:00Z', 60),
    ride('r2', '2026-05-01T08:00:00Z', 42),
    ride('r3', '2026-04-01T08:00:00Z', 88),
  ];

  it('lists every ride except the excluded (current) one', () => {
    const { el } = setup(rides, 'r1');

    const rows = el.querySelectorAll('[data-picker-row]');
    expect(rows.length).toBe(2);
    expect(el.textContent).not.toContain('r1'); // excluded
  });

  it('filters the list by the search query', () => {
    const { el, fixture } = setup(rides);

    const search = el.querySelector('[data-testid="picker-search"]') as HTMLInputElement;
    search.value = '88';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const rows = el.querySelectorAll('[data-picker-row]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('88');
  });

  it('emits the chosen ride when a row is clicked', () => {
    const { el, host } = setup(rides, 'r1');

    (el.querySelector('[data-picker-row]') as HTMLElement).click();

    expect(host.picked?.id).toBe('r2'); // first non-excluded row
  });

  it('emits close when the close control is used', () => {
    const { el, host } = setup(rides);

    (el.querySelector('[data-picker-close]') as HTMLButtonElement).click();

    expect(host.closed).toBe(true);
  });
});
