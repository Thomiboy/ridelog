import { Component, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ContactService } from '../../core/api/contact.service';
import type { ContactMessage } from '../../core/api/contact.models';

/**
 * The owner's small contact inbox: a flat list with delete, and the kill switch. Deliberately not a
 * threaded inbox — the mail carries the message, so the owner reads and replies from their own client,
 * and this is the net under a failed send and the place to clear what has been dealt with (#168).
 */
@Component({
  selector: 'app-messages',
  imports: [TranslocoPipe, TranslocoDatePipe, MatButtonModule, MatIconModule],
  templateUrl: './messages.html',
  styleUrl: './messages.scss',
})
export class Messages {
  private readonly contact = inject(ContactService);

  readonly messages = signal<ContactMessage[]>([]);
  readonly accepting = signal(true);

  constructor() {
    this.reload();
    this.contact.getConfig().subscribe((config) => this.accepting.set(config.enabled));
  }

  toggle(event: Event): void {
    const enabled = (event.target as HTMLInputElement).checked;
    this.contact.setEnabled(enabled).subscribe(() => this.accepting.set(enabled));
  }

  delete(id: string): void {
    this.contact.deleteMessage(id).subscribe(() => this.reload());
  }

  private reload(): void {
    this.contact.getMessages().subscribe((messages) => this.messages.set(messages));
  }
}
