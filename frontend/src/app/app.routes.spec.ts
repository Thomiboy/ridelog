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
  function configure(admin: boolean) {
    TestBed.configureTestingModule({
      imports: [translocoTesting()],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: { isAdmin: signal(admin), isLoggedIn: signal(admin), logout: () => {} },
        },
      ],
    });
  }

  async function navigate(url: string, admin = false) {
    configure(admin);
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
    expect(await navigate('/admin', true)).toContain('Admin');
  });

  it('redirects non-admins away from the admin page', async () => {
    expect(await navigate('/admin', false)).toContain('Log in');
  });
});
