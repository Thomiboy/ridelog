import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { jwtInterceptor } from './jwt.interceptor';
import { environment } from '../../../environments/environment';

describe('jwtInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  function configure() {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([jwtInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('attaches the bearer token to API requests when logged in', () => {
    localStorage.setItem('ridelog.token', 'jwt-xyz');
    configure();

    http.get(`${environment.apiBaseUrl}/rides`).subscribe();

    const request = controller.expectOne(`${environment.apiBaseUrl}/rides`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-xyz');
    request.flush([]);
  });

  it('does not attach a token when logged out', () => {
    localStorage.clear();
    configure();

    http.get(`${environment.apiBaseUrl}/rides`).subscribe();

    const request = controller.expectOne(`${environment.apiBaseUrl}/rides`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush([]);
  });

  it('never sends the token to a non-API host', () => {
    localStorage.setItem('ridelog.token', 'jwt-xyz');
    configure();

    http.get('https://tile.openstreetmap.org/1/2/3.png').subscribe();

    const request = controller.expectOne('https://tile.openstreetmap.org/1/2/3.png');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush('');
  });
});
