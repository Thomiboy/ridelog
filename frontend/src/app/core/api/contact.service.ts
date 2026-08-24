import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type { ContactConfig, ContactMessage, ContactSubmission } from './contact.models';

/**
 * The contact form's client. The public half — read the config, submit a message — needs no sign-in;
 * the owner's half — list, delete, flip the kill switch — is admin-only and refused otherwise.
 */
@Injectable({ providedIn: 'root' })
export class ContactService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getConfig(): Observable<ContactConfig> {
    return this.http.get<ContactConfig>(`${this.baseUrl}/contact`);
  }

  submit(submission: ContactSubmission): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/contact`, submission);
  }

  getMessages(): Observable<ContactMessage[]> {
    return this.http.get<ContactMessage[]>(`${this.baseUrl}/messages`);
  }

  deleteMessage(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/messages/${id}`);
  }

  /** The kill switch: turns submissions on or off. */
  setEnabled(enabled: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/contact/switch`, { enabled });
  }
}
