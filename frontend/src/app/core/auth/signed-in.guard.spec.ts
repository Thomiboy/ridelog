import { TestBed } from '@angular/core/testing';
import { Router, type ActivatedRouteSnapshot, type RouterStateSnapshot } from '@angular/router';
import { vi } from 'vitest';
import { signedInGuard } from './signed-in.guard';
import { AuthService } from './auth.service';

describe('signedInGuard', () => {
  function run(isLoggedIn: boolean) {
    const router = { createUrlTree: vi.fn().mockReturnValue('LOGIN_URL_TREE') };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isLoggedIn: () => isLoggedIn } },
        { provide: Router, useValue: router },
      ],
    });
    const result = TestBed.runInInjectionContext(() =>
      signedInGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
    return { result, router };
  }

  /** Being an admin is not what this asks: the page behind it is a rider's own log. */
  it('allows any signed-in rider through', () => {
    expect(run(true).result).toBe(true);
  });

  it('redirects a visitor to the login page', () => {
    const { result, router } = run(false);

    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(result).toBe('LOGIN_URL_TREE');
  });
});
