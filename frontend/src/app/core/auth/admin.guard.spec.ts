import { TestBed } from '@angular/core/testing';
import { Router, type ActivatedRouteSnapshot, type RouterStateSnapshot } from '@angular/router';
import { vi } from 'vitest';
import { adminGuard } from './admin.guard';
import { AuthService } from './auth.service';

describe('adminGuard', () => {
  function run(isAdmin: boolean) {
    const router = { createUrlTree: vi.fn().mockReturnValue('LOGIN_URL_TREE') };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAdmin: () => isAdmin } },
        { provide: Router, useValue: router },
      ],
    });
    const result = TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
    return { result, router };
  }

  it('allows an admin through', () => {
    expect(run(true).result).toBe(true);
  });

  it('redirects a non-admin to the login page', () => {
    const { result, router } = run(false);

    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(result).toBe('LOGIN_URL_TREE');
  });
});
