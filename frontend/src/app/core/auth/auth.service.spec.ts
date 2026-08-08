import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores the token and reports logged in after a successful login', () => {
    let completed = false;
    service.login('admin@ridelog.test', 'pw').subscribe(() => (completed = true));

    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush({
      token: 'jwt-token-abc',
      expiresAt: '2030-01-01T00:00:00Z',
    });
    http.expectOne(`${environment.apiBaseUrl}/auth/me`).flush({ email: 'admin@ridelog.test', roles: ['Admin'] });

    expect(completed).toBe(true);
    expect(service.isLoggedIn()).toBe(true);
    expect(service.token()).toBe('jwt-token-abc');
  });

  it('is logged out by default', () => {
    expect(service.isLoggedIn()).toBe(false);
    expect(service.token()).toBeNull();
  });

  it('reports admin when the profile carries the Admin role', () => {
    service.login('admin@ridelog.test', 'pw').subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush({ token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    http.expectOne(`${environment.apiBaseUrl}/auth/me`).flush({ email: 'admin@ridelog.test', roles: ['Admin'] });

    expect(service.isAdmin()).toBe(true);
  });

  it('is not admin without the Admin role', () => {
    service.login('user@ridelog.test', 'pw').subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush({ token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    http.expectOne(`${environment.apiBaseUrl}/auth/me`).flush({ email: 'user@ridelog.test', roles: [] });

    expect(service.isAdmin()).toBe(false);
  });

  it('sends a rider to the provider through the API, which is what holds the client id', () => {
    expect(service.authorizeUrl('google')).toBe(`${environment.apiBaseUrl}/auth/google/authorize`);
  });

  it('exchanges the code the provider callback came back with for a token', () => {
    let completed = false;
    service.completeExternalSignIn('one-time-code').subscribe(() => (completed = true));

    const exchange = http.expectOne(`${environment.apiBaseUrl}/auth/exchange`);
    expect(exchange.request.body).toEqual({ code: 'one-time-code' });
    exchange.flush({ token: 'jwt-token-xyz', expiresAt: '2030-01-01T00:00:00Z' });
    http.expectOne(`${environment.apiBaseUrl}/auth/me`).flush({ email: 'rider@example.test', roles: [] });

    expect(completed).toBe(true);
    expect(service.token()).toBe('jwt-token-xyz');
    expect(service.isAdmin()).toBe(false);
  });

  it('clears token and profile on logout', () => {
    service.login('admin@ridelog.test', 'pw').subscribe();
    http.expectOne(`${environment.apiBaseUrl}/auth/login`).flush({ token: 't', expiresAt: '2030-01-01T00:00:00Z' });
    http.expectOne(`${environment.apiBaseUrl}/auth/me`).flush({ email: 'admin@ridelog.test', roles: ['Admin'] });

    service.logout();

    expect(service.isLoggedIn()).toBe(false);
    expect(service.isAdmin()).toBe(false);
    expect(service.token()).toBeNull();
  });
});
