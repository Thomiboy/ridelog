import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { Login } from './login';
import { AuthService } from '../../core/auth/auth.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Login', () => {
  function setup(loginResult: ReturnType<AuthService['login']>) {
    const auth = { login: vi.fn().mockReturnValue(loginResult) };
    const router = { navigateByUrl: vi.fn() };
    TestBed.configureTestingModule({
      imports: [Login, translocoTesting()],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, auth, router };
  }

  it('logs in and navigates home on success', () => {
    const { component, auth, router } = setup(of({ email: 'admin@ridelog.test', roles: ['Admin'] }));

    component.form.setValue({ email: 'admin@ridelog.test', password: 'pw' });
    component.submit();

    expect(auth.login).toHaveBeenCalledWith('admin@ridelog.test', 'pw');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows an error message when login fails', () => {
    const { fixture, component } = setup(throwError(() => new Error('unauthorized')));

    component.form.setValue({ email: 'admin@ridelog.test', password: 'wrong' });
    component.submit();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Login failed');
  });

  it('does not call the API when the form is empty', () => {
    const { component, auth } = setup(of({ email: '', roles: [] }));

    component.submit();

    expect(auth.login).not.toHaveBeenCalled();
  });
});
