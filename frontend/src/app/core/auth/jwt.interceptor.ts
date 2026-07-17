import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

/**
 * Attaches the bearer token to requests aimed at our own API only, so the token is never leaked to
 * third-party hosts (e.g. map tile servers).
 */
export const jwtInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).token();

  if (token && request.url.startsWith(environment.apiBaseUrl)) {
    return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }

  return next(request);
};
