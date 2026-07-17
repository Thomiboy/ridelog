import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService } from './admin.service';
import { environment } from '../../../environments/environment';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('fetches the Polar authorize url', () => {
    let url: string | undefined;
    service.getPolarAuthorizeUrl().subscribe((r) => (url = r.authorizeUrl));

    const request = http.expectOne(`${environment.apiBaseUrl}/polar/authorize`);
    expect(request.request.method).toBe('GET');
    request.flush({ authorizeUrl: 'https://flow.polar.com/oauth2/authorization?x=1' });

    expect(url).toBe('https://flow.polar.com/oauth2/authorization?x=1');
  });

  it('triggers a sync', () => {
    let summary: { imported: number } | undefined;
    service.sync().subscribe((s) => (summary = s));

    const request = http.expectOne(`${environment.apiBaseUrl}/sync`);
    expect(request.request.method).toBe('POST');
    request.flush({ imported: 2, skipped: 0, failed: 0 });

    expect(summary!.imported).toBe(2);
  });

  it('uploads ride files as multipart form data', () => {
    const file = new File(['<gpx/>'], 'ride.gpx', { type: 'application/gpx+xml' });

    service.importRides([file]).subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/import`);
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(body instanceof FormData).toBe(true);
    expect(body.getAll('files').length).toBe(1);
    request.flush({ files: [], imported: 1, skipped: 0, failed: 0 });
  });
});
