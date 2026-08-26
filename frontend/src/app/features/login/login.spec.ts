import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Login } from './login';
import { AuthService } from '../../core/auth/auth.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('Login', () => {
  function setup(loginResult: ReturnType<AuthService['login']>, query: Record<string, string> = {}) {
    const auth = {
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

  /**
   * Registration is open, so signing in and being let in are different things. A rider who has only
   * knocked comes back here with no code — landing on a blank login form would read as a failure.
   */
  it('says the owner has to let you in when the callback comes back pending', () => {
    const { fixture, auth } = setup(of({ email: '', roles: [] }), { status: 'pending' });

    expect(auth.completeExternalSignIn).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('waiting for approval');
  });

  /**
   * The password is the seeded admin's break-glass key and nobody else's — a new rider has none and
   * never will (docs/adr/0007). Offering everyone a field they can never fill is noise, so the form
   * lives at `/login/password` (#186). It is moved, not hidden: the guard is on the endpoint.
   */
  it('does not offer a password form', () => {
    const { fixture } = setup(of({ email: '', roles: [] }));
    const page = fixture.nativeElement as HTMLElement;

    expect(page.querySelector('input[type="password"]')).toBeNull();
    expect(page.querySelector('form')).toBeNull();
  });
});
