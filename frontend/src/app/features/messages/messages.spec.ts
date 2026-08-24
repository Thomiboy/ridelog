import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Messages } from './messages';
import { ContactService } from '../../core/api/contact.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import type { ContactMessage } from '../../core/api/contact.models';

describe('Messages', () => {
  const message = (id: string): ContactMessage => ({
    id,
    senderName: `Name ${id}`,
    senderEmail: `${id}@example.test`,
    message: `body ${id}`,
    submittedAt: '2026-08-24T09:00:00Z',
  });

  function render(messages: ContactMessage[], enabled = true) {
    const service = {
      getMessages: vi.fn().mockReturnValue(of(messages)),
      getConfig: vi.fn().mockReturnValue(of({ enabled, ownerEmail: null })),
      deleteMessage: vi.fn().mockReturnValue(of(undefined)),
      setEnabled: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      imports: [Messages, translocoTesting()],
      providers: [{ provide: ContactService, useValue: service }],
    });
    const fixture = TestBed.createComponent(Messages);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, service };
  }

  it('lists the stored messages', () => {
    const { el } = render([message('1'), message('2')]);

    expect(el.querySelectorAll('[data-message]').length).toBe(2);
    expect(el.textContent).toContain('body 1');
  });

  it('deletes a message and refreshes the list', () => {
    const { el, service } = render([message('1')]);

    (el.querySelector('[data-delete]') as HTMLButtonElement).click();

    expect(service.deleteMessage).toHaveBeenCalledWith('1');
    // The initial load plus the reload after deleting.
    expect(service.getMessages).toHaveBeenCalledTimes(2);
  });

  it('flips the kill switch off', () => {
    const { el, service } = render([], true);
    const toggle = el.querySelector('[data-switch]') as HTMLInputElement;

    toggle.checked = false;
    toggle.dispatchEvent(new Event('change'));

    expect(service.setEnabled).toHaveBeenCalledWith(false);
  });
});
