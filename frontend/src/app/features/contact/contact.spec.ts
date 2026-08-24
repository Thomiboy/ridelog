import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Contact } from './contact';
import { ContactService } from '../../core/api/contact.service';
import { translocoTesting } from '../../core/i18n/transloco-testing';
import type { ContactConfig } from '../../core/api/contact.models';

describe('Contact', () => {
  function render(config: ContactConfig, submit = vi.fn().mockReturnValue(of(undefined))) {
    const service = { getConfig: vi.fn().mockReturnValue(of(config)), submit };
    TestBed.configureTestingModule({
      imports: [Contact, translocoTesting()],
      providers: [{ provide: ContactService, useValue: service }],
    });
    const fixture = TestBed.createComponent(Contact);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, service };
  }

  it('offers a form with a hidden honeypot when the form is on', () => {
    const { el } = render({ enabled: true, ownerEmail: null });

    expect(el.querySelector('form')).toBeTruthy();
    const honeypot = el.querySelector('[data-honeypot]');
    expect(honeypot).toBeTruthy();
    expect(honeypot!.getAttribute('aria-hidden')).toBe('true');
  });

  it('submits the message and confirms it was sent', () => {
    const { fixture, el, service } = render({ enabled: true, ownerEmail: null });
    fixture.componentInstance.form.setValue({
      name: 'Visitor',
      email: 'v@example.test',
      message: 'Hello there',
      website: '',
    });

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(service.submit).toHaveBeenCalledWith({
      name: 'Visitor',
      email: 'v@example.test',
      message: 'Hello there',
      website: '',
    });
    expect(el.querySelector('[data-sent]')).toBeTruthy();
  });

  it('shows the owner address instead of a form when the form is off', () => {
    const { el } = render({ enabled: false, ownerEmail: 'owner@ridelog.test' });

    expect(el.querySelector('form')).toBeNull();
    const link = el.querySelector('[data-owner-email]') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('mailto:owner@ridelog.test');
  });
});
