import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AuthService } from '../../core/auth/auth.service';

/**
 * The seeded admin's password, on a route nothing links to (#186). It is the break-glass key: the
 * only way in when a provider is unreachable or the linked account is lost, and there is no reset
 * behind it, because nothing here can mail a rider (docs/adr/0007 amendment). Unadvertised is not
 * protection — an Angular route ships in the bundle — so the guard that matters is the rate limit
 * on the endpoint; moving the form off `/login` only spares the riders who can never use it.
 */
@Component({
  selector: 'app-password-login',
  imports: [ReactiveFormsModule, TranslocoPipe, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './password-login.html',
  styleUrl: './password-login.scss',
})
export class PasswordLogin {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly failed = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.failed.set(false);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: () => this.failed.set(true),
    });
  }
}
