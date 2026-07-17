import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

const TOKEN_KEY = 'ridelog.token';

interface LoginResponse {
  token: string;
  expiresAt: string;
}

interface Profile {
  email: string;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly _profile = signal<Profile | null>(null);

  readonly token = this._token.asReadonly();
  readonly profile = this._profile.asReadonly();
  readonly isLoggedIn = computed(() => this._token() !== null);
  readonly isAdmin = computed(() => this._profile()?.roles.includes('Admin') ?? false);

  login(email: string, password: string): Observable<Profile> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/auth/login`, { email, password }).pipe(
      tap((response) => this.setToken(response.token)),
      switchMap(() => this.loadProfile()),
    );
  }

  loadProfile(): Observable<Profile> {
    return this.http
      .get<Profile>(`${this.baseUrl}/auth/me`)
      .pipe(tap((profile) => this._profile.set(profile)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this._token.set(null);
    this._profile.set(null);
  }

  private setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this._token.set(token);
  }
}
