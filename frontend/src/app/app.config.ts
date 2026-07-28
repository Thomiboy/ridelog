import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { routes } from './app.routes';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { AuthService } from './core/auth/auth.service';
import { translocoProviders } from './core/i18n/transloco-providers';
import { translocoLocaleProviders } from './core/i18n/transloco-locale-providers';
import { LanguageService } from './core/i18n/language.service';
import { ThemeService } from './core/theme/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor])),
    provideCharts(withDefaultRegisterables()),
    translocoProviders,
    translocoLocaleProviders,
    // Apply the saved theme before the first paint (its constructor sets the root color-scheme).
    provideAppInitializer(() => {
      inject(ThemeService);
    }),
    // Apply the saved UI language (and preload it) before the first paint, so there's no flash.
    provideAppInitializer(() => inject(LanguageService).init()),
    // Restore the logged-in profile on startup when a token is already present.
    provideAppInitializer(() => {
      const auth = inject(AuthService);
      if (auth.token()) {
        auth.loadProfile().subscribe({ error: () => auth.logout() });
      }
    }),
  ],
};
