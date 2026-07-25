import { UpperCasePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';
import { LANGUAGES, LanguageService, type Language } from '../../core/i18n/language.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive, TranslocoPipe, UpperCasePipe, MatButtonModule],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);

  readonly isLoggedIn = this.auth.isLoggedIn;
  readonly isAdmin = this.auth.isAdmin;

  readonly languages = LANGUAGES;
  readonly activeLanguage = this.language.current;

  useLanguage(lang: Language): void {
    this.language.use(lang);
  }

  logout(): void {
    this.auth.logout();
  }
}
