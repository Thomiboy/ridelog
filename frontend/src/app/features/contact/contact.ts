import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ContactService } from '../../core/api/contact.service';
import { ContactLimits } from '../../core/api/contact.limits';
import type { ContactConfig } from '../../core/api/contact.models';

/**
 * The public contact page. While the form is on it shows the form; switched off it shows the owner's
 * address instead, with a line saying the form is temporarily unavailable — a page that announces an
 * outage is worse than one that just says how to get in touch (#168).
 */
@Component({
  selector: 'app-contact',
  imports: [ReactiveFormsModule, TranslocoPipe, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './contact.html',
  styleUrl: './contact.scss',
})
export class Contact {
  private readonly fb = inject(FormBuilder);
  private readonly contact = inject(ContactService);

  readonly limits = ContactLimits;
  readonly config = signal<ContactConfig | null>(null);
  readonly sent = signal(false);
  readonly failed = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(ContactLimits.nameMax)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(ContactLimits.emailMax)]],
    message: ['', [Validators.required, Validators.maxLength(ContactLimits.messageMax)]],
    // The honeypot: hidden from people, tempting to bots, and left empty by any real visitor.
    website: [''],
  });

  constructor() {
    this.contact.getConfig().subscribe((config) => this.config.set(config));
  }

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.failed.set(false);
    this.contact.submit(this.form.getRawValue()).subscribe({
      next: () => {
        this.sent.set(true);
        this.form.reset();
      },
      error: () => this.failed.set(true),
    });
  }
}
