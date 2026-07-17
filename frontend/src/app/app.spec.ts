import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { translocoTesting } from './core/i18n/transloco-testing';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App, translocoTesting()],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isLoggedIn: signal(false), isAdmin: signal(false), logout: () => {} } },
      ],
    });
  });

  it('creates the app', () => {
    expect(TestBed.createComponent(App).componentInstance).toBeTruthy();
  });

  it('renders the header with the app brand', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('RideLog');
  });
});
