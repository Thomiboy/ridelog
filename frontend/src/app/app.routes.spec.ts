import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { RideDetail } from './features/ride-detail/ride-detail';
import { translocoTesting } from './core/i18n/transloco-testing';

describe('app routing', () => {
  function configure(admin: boolean, signedIn = admin) {
    TestBed.configureTestingModule({
      imports: [translocoTesting()],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            isAdmin: signal(admin),
            isLoggedIn: signal(signedIn),
            logout: () => {},
            authorizeUrl: (provider: string) => `/auth/${provider}/authorize`,
          },
        },
      ],
    });
  }

  async function navigate(url: string, admin = false, signedIn = admin) {
    configure(admin, signedIn);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    return harness.routeNativeElement?.textContent ?? '';
  }

  it('serves the dashboard at the root', async () => {
    expect(await navigate('/')).toContain('Dashboard');
  });

  it('serves the rides list', async () => {
    expect(await navigate('/rides')).toContain('Rides');
  });

  // Other activities get a page of their own rather than a tab inside the rides list, because they
  // are a sibling of rides and not a view over them.
  it('serves the other activities list', async () => {
    expect(await navigate('/activities')).toContain('Other activity');
  });

  it('serves the ride detail', async () => {
    configure(false);
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/rides/abc', RideDetail);
    expect(component).toBeInstanceOf(RideDetail);
  });

  it('serves the login page', async () => {
    expect(await navigate('/login')).toContain('Log in');
  });

  it('serves the admin page to admins', async () => {
    expect(await navigate('/admin', true)).toContain('Polar');
  });

  /**
   * Nearly everything on this page is about the rider's own log — their Polar link, their zones,
   * their rides. Only the bulk import crosses riders, and that is hidden rather than the page.
   */
  it('serves the page to an ordinary signed-in rider', async () => {
    expect(await navigate('/admin', false, true)).toContain('Polar');
  });

  it('sends a visitor who is not signed in to the login page', async () => {
    expect(await navigate('/admin', false, false)).toContain('Log in');
  });
});
