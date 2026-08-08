import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, TranslocoPipe, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Which message to show, if any — the two ways in fail for different reasons. */
  readonly error = signal<'login.error' | 'login.externalError' | null>(null);

  /**
   * New riders arrive through a provider; the password form is the seeded admin's way in. The names
   * are not translated — they are the providers' own.
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
        error: () => this.error.set('login.externalError'),
      });
    } else if (query.get('error')) {
      this.error.set('login.externalError');
    }
  }

  authorizeUrl(provider: string): string {
    return this.auth.authorizeUrl(provider);
  }

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: () => this.error.set('login.error'),
    });
  }
}
