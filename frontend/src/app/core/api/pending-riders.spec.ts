import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { PendingRiders } from './pending-riders';
import { RidersService, type RiderSummary } from './riders.service';
import { AuthService } from '../auth/auth.service';

describe('PendingRiders', () => {
  const rider = (id: string, approval: RiderSummary['approval']): RiderSummary => ({
    id,
    email: `${id}@example.test`,
    approval,
    rideCount: 0,
    storageBytes: 0,
    polarLinked: false,
    lastSyncAt: null,
    isPublicLog: false,
  });

  function setup(riders: RiderSummary[], isAdmin = true) {
    const ridersService = { list: vi.fn().mockReturnValue(of(riders)) };
    TestBed.configureTestingModule({
      providers: [
        { provide: RidersService, useValue: ridersService },
        { provide: AuthService, useValue: { isAdmin: signal(isAdmin) } },
      ],
    });
    return { service: TestBed.inject(PendingRiders), ridersService };
  }

  /** Rejected riders have been decided about; counting them would keep the badge lit forever. */
  it('counts only the riders still waiting on a decision', () => {
    const { service } = setup([
      rider('a', 'Pending'),
      rider('b', 'Pending'),
      rider('c', 'Approved'),
      rider('d', 'Rejected'),
    ]);
    TestBed.flushEffects();

    expect(service.pending()).toBe(2);
  });

  it('asks for nothing when the reader is not an admin', () => {
    const { service, ridersService } = setup([rider('a', 'Pending')], false);
    TestBed.flushEffects();

    expect(ridersService.list).not.toHaveBeenCalled();
    expect(service.pending()).toBe(0);
  });

  /** A count nobody asked for must never be the thing that breaks a page. */
  it('shows nothing rather than failing when the list cannot be read', () => {
    const ridersService = { list: vi.fn().mockReturnValue(throwError(() => new Error('offline'))) };
    TestBed.configureTestingModule({
      providers: [
        { provide: RidersService, useValue: ridersService },
        { provide: AuthService, useValue: { isAdmin: signal(true) } },
      ],
    });
    const service = TestBed.inject(PendingRiders);
    TestBed.flushEffects();

    expect(service.pending()).toBe(0);
  });
});
