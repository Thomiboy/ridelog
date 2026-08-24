import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ContactService } from './contact.service';
import { environment } from '../../../environments/environment';

describe('ContactService', () => {
  let service: ContactService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ContactService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the public contact config', () => {
    let enabled: boolean | undefined;
    service.getConfig().subscribe((c) => (enabled = c.enabled));

    const request = http.expectOne(`${environment.apiBaseUrl}/contact`);
    expect(request.request.method).toBe('GET');
    request.flush({ enabled: false, ownerEmail: 'owner@ridelog.test' });

    expect(enabled).toBe(false);
  });

  it('submits a message with the honeypot field carried through', () => {
    service
      .submit({ name: 'Visitor', email: 'v@example.test', message: 'Hi', website: 'spam' })
      .subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/contact`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Visitor',
      email: 'v@example.test',
      message: 'Hi',
      website: 'spam',
    });
    request.flush(null);
  });

  it('lists stored messages for the owner', () => {
    let count: number | undefined;
    service.getMessages().subscribe((m) => (count = m.length));

    const request = http.expectOne(`${environment.apiBaseUrl}/messages`);
    expect(request.request.method).toBe('GET');
    request.flush([
      { id: '1', senderName: 'A', senderEmail: 'a@x.test', message: 'hi', submittedAt: '2026-08-24T00:00:00Z' },
    ]);

    expect(count).toBe(1);
  });

  it('deletes a stored message by id', () => {
    service.deleteMessage('abc').subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/messages/abc`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });

  it('flips the kill switch', () => {
    service.setEnabled(false).subscribe();

    const request = http.expectOne(`${environment.apiBaseUrl}/contact/switch`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ enabled: false });
    request.flush(null);
  });
});
