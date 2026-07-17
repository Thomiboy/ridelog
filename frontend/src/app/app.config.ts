import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { AuthService } from './core/auth/auth.service';
import { translocoProviders } from './core/i18n/transloco-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor])),
    translocoProviders,
    // Restore the logged-in profile on startup when a token is already present.
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      if (auth.token()) {
        auth.loadProfile().subscribe({ error: () => auth.logout() });
      }
    }),
  ],
};
