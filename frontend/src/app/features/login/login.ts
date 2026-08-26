import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';

/**
 * The way in for everyone who has one: the sign-in providers, plus whatever the round trip came
 * back saying. The seeded admin's password form is not here — it moved to `/login/password` (#186),
 * because a new rider has no password and never will (docs/adr/0007).
 */
@Component({
  selector: 'app-login',
  imports: [TranslocoPipe, MatButtonModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Set when the provider would not vouch for the rider, or the exchange failed. */
  readonly failed = signal(false);

  /** Set when the provider knew who the rider is but the owner has not let them in yet. */
  readonly waiting = signal(false);

  /**
   * New riders arrive through a provider. The names are not translated — they are the providers' own.
   */
  readonly providers = [
    { id: 'google', name: 'Google' },
    { id: 'microsoft', name: 'Microsoft' },
  ];

  constructor() {
    // The provider's callback sends the rider back here, carrying either a code worth a token or
    // the reason it refused.
    const query = this.route.snapshot.queryParamMap;
    const code = query.get('code');
    if (code) {
      this.auth.completeExternalSignIn(code).subscribe({
        next: () => this.router.navigateByUrl('/'),
        error: () => this.failed.set(true),
      });
    } else if (query.get('status') === 'pending') {
      // The provider knew them; the owner has not decided. Not a failure, and not a way in either.
      this.waiting.set(true);
    } else if (query.get('error')) {
      this.failed.set(true);
    }
  }

  authorizeUrl(provider: string): string {
    return this.auth.authorizeUrl(provider);
  }
}
