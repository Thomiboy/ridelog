import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { Login } from './login';
import { AuthService } from '../../core/auth/auth.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Login', () => {
  function setup(loginResult: ReturnType<AuthService['login']>, query: Record<string, string> = {}) {
    const auth = {
      login: vi.fn().mockReturnValue(loginResult),
      authorizeUrl: vi.fn((provider: string) => `https://api.test/auth/${provider}/authorize`),
      completeExternalSignIn: vi.fn().mockReturnValue(loginResult),
    };
    const router = { navigateByUrl: vi.fn() };
    const route = { snapshot: { queryParamMap: convertToParamMap(query) } };
    TestBed.configureTestingModule({
      imports: [Login, translocoTesting()],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: route },
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

  /**
   * A plain link, not a click handler: the sign-in round trip is a browser navigation to the API,
   * which is what holds the client id and the redirect the provider has registered.
   */
  it('offers a link to each provider', () => {
    const { fixture } = setup(of({ email: '', roles: [] }));

    const links = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('a[href]');

    expect([...links].map((link) => link.getAttribute('href'))).toEqual([
      'https://api.test/auth/google/authorize',
      'https://api.test/auth/microsoft/authorize',
    ]);
  });

  it('exchanges the code the provider callback left in the URL and navigates home', () => {
    const { auth, router } = setup(of({ email: 'rider@example.test', roles: [] }), { code: 'one-time-code' });

    expect(auth.completeExternalSignIn).toHaveBeenCalledWith('one-time-code');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  /**
   * The callback refuses by sending the rider back here with a reason, so the page has to say
   * something — landing on a blank login form after signing in reads as the app losing the attempt.
   */
  it('says so when the callback came back refused', () => {
    const { fixture, auth } = setup(of({ email: '', roles: [] }), { error: 'refused' });

    expect(auth.completeExternalSignIn).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Login failed');
  });

  it('does not call the API when the form is empty', () => {
    const { component, auth } = setup(of({ email: '', roles: [] }));

    component.submit();

    expect(auth.login).not.toHaveBeenCalled();
  });
});
