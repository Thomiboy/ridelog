import { UpperCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/auth/auth.service';
import { LANGUAGES, LanguageService, type Language } from '../../core/i18n/language.service';
import { THEME_PREFERENCES, ThemeService, type ThemePreference } from '../../core/theme/theme.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, TranslocoPipe, UpperCasePipe, MatButtonModule, MatIconModule],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);
  private readonly theme = inject(ThemeService);

  readonly isLoggedIn = this.auth.isLoggedIn;
  readonly isAdmin = this.auth.isAdmin;

  readonly languages = LANGUAGES;
  readonly activeLanguage = this.language.current;

  readonly themes = THEME_PREFERENCES;
  readonly activeTheme = this.theme.preference;
  /** Material icon per theme preference. */
  readonly themeIcons: Record<ThemePreference, string> = {
    system: 'brightness_auto',
    light: 'light_mode',
    dark: 'dark_mode',
  };

  useLanguage(lang: Language): void {
    this.language.use(lang);
  }

  useTheme(preference: ThemePreference): void {
    this.theme.use(preference);
  }

  logout(): void {
    this.auth.logout();
  }
}
