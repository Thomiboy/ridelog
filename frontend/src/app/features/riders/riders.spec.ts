import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Riders } from './riders';
import { PendingRiders } from '../../core/api/pending-riders';
import { RidersService } from '../../core/api/riders.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Riders', () => {
  const rider = (
    id: string,
    email: string,
    approval: 'Pending' | 'Approved' | 'Rejected',
    extra = {},
  ) => ({
    id,
    email,
    approval,
    rideCount: 0,
    storageBytes: 0,
    polarLinked: false,
    lastSyncAt: null,
    isPublicLog: false,
    ...extra,
  });

  const knocking = rider('r1', 'knocking@example.test', 'Pending');
  const inside = rider('r2', 'inside@example.test', 'Approved', {
    rideCount: 12,
    storageBytes: 3_500_000,
    polarLinked: true,
  });

  function setup(riders = [knocking, inside]) {
    const ridersService = {
      list: vi.fn().mockReturnValue(of(riders)),
      setApproval: vi.fn().mockReturnValue(of(void 0)),
      setPublicLog: vi.fn().mockReturnValue(of(void 0)),
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
   * The column the page exists for: raw files share one 32 GB database, and a list of names cannot
   * say where it went. Bytes are shown as a size a person reads, not as a number of bytes.
   */
  it('says what each rider is using', () => {
    const { el } = setup();

    const row = el.querySelector('[data-rider="r2"]')!;
    expect(row.textContent).toContain('12');
    expect(row.textContent).toContain('3.5 MB');
  });

  /**
   * #159 refuses to close this rider's account, so the list has to say which one they are. Only an
   * approved rider can take it over — one who is not in cannot be the face of the site.
   */
  it('marks the public log and moves it to another approved rider', () => {
    const other = rider('r3', 'other@example.test', 'Approved');
    const { el, ridersService } = setup([knocking, { ...inside, isPublicLog: true }, other]);

    expect(el.querySelector('[data-rider="r2"] [data-public-log]')).not.toBeNull();
    // The rider who already holds it is not offered the button again.
    expect(el.querySelector('[data-rider="r2"] [data-make-public]')).toBeNull();
    // Nor is one who has only knocked.
    expect(el.querySelector('[data-rider="r1"] [data-make-public]')).toBeNull();

    (el.querySelector('[data-rider="r3"] [data-make-public]') as HTMLButtonElement).click();
    expect(ridersService.setPublicLog).toHaveBeenCalledWith('r3');
  });

  /**
   * The refusals come back from the API as 409 with a reason. A generic "something went wrong"
   * would send the owner hunting for a bug instead of reading the sentence.
   */
  it('shows the reason when the API refuses', () => {
    const ridersService = {
      list: vi.fn().mockReturnValue(of([inside])),
      setPublicLog: vi.fn().mockReturnValue(of(void 0)),
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

    (
      fixture.nativeElement.querySelector('[data-rider="r2"] [data-reject]') as HTMLButtonElement
    ).click();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'cannot shut yourself out',
    );
  });
});
