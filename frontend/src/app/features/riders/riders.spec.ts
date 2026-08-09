import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Riders } from './riders';
import { PendingRiders } from '../../core/api/pending-riders';
import { RidersService } from '../../core/api/riders.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Riders', () => {
  const knocking = { id: 'r1', email: 'knocking@example.test', approval: 'Pending' as const };
  const inside = { id: 'r2', email: 'inside@example.test', approval: 'Approved' as const };

  function setup(riders = [knocking, inside]) {
    const ridersService = {
      list: vi.fn().mockReturnValue(of(riders)),
      setApproval: vi.fn().mockReturnValue(of(void 0)),
    };
    TestBed.configureTestingModule({
      imports: [Riders, translocoTesting()],
      providers: [
        { provide: RidersService, useValue: ridersService },
        // Stubbed so the reload assertions stay about this page; the badge has its own tests.
        { provide: PendingRiders, useValue: { refreshPending: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(Riders);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, ridersService };
  }

  it('lists every rider with where they stand', () => {
    const { el } = setup();

    expect(el.querySelectorAll('[data-rider]').length).toBe(2);
    expect(el.textContent).toContain('knocking@example.test');
    expect(el.textContent).toContain('inside@example.test');
  });

  /**
   * The whole point of the page: turning somebody who knocked into somebody who is in. The list
   * reloads afterwards, so the row shows what the server now believes rather than what was clicked.
   */
  it('lets a pending rider in and reloads the list', () => {
    const { el, ridersService } = setup();

    (el.querySelector('[data-rider="r1"] [data-approve]') as HTMLButtonElement).click();

    expect(ridersService.setApproval).toHaveBeenCalledWith('r1', 'Approved');
    expect(ridersService.list).toHaveBeenCalledTimes(2);
  });

  /** Rejecting an approved rider is the ban — the same control, thrown the other way. */
  it('shuts an approved rider out', () => {
    const { el, ridersService } = setup();

    (el.querySelector('[data-rider="r2"] [data-reject]') as HTMLButtonElement).click();

    expect(ridersService.setApproval).toHaveBeenCalledWith('r2', 'Rejected');
  });

  /**
   * The refusals come back from the API as 409 with a reason. A generic "something went wrong"
   * would send the owner hunting for a bug instead of reading the sentence.
   */
  it('shows the reason when the API refuses', () => {
    const ridersService = {
      list: vi.fn().mockReturnValue(of([inside])),
      setApproval: vi.fn().mockReturnValue(
        new (class {
          subscribe(observer: { error: (e: unknown) => void }) {
            observer.error({ status: 409, error: 'You cannot shut yourself out.' });
            return { unsubscribe: () => {} };
          }
        })(),
      ),
    };
    TestBed.configureTestingModule({
      imports: [Riders, translocoTesting()],
      providers: [
        { provide: RidersService, useValue: ridersService },
        { provide: PendingRiders, useValue: { refreshPending: vi.fn() } },
      ],
    });
    const fixture = TestBed.createComponent(Riders);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-rider="r2"] [data-reject]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('cannot shut yourself out');
  });
});
