import { Component, input, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { RouteMap } from './features/ride-detail/route-map/route-map';
import { translocoTesting } from './core/i18n/transloco-testing';

// Leaflet needs a real canvas; stub the background map so the shell renders in jsdom.
@Component({ selector: 'app-route-map', template: '' })
class RouteMapStub {
  readonly polyline = input<string | null | undefined>();
}

describe('App', () => {
  function setup() {
    TestBed.configureTestingModule({
      imports: [App, translocoTesting()],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isLoggedIn: signal(false), isAdmin: signal(false), logout: () => {} } },
      ],
    }).overrideComponent(App, {
      remove: { imports: [RouteMap] },
      add: { imports: [RouteMapStub] },
    });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('creates the app shell', () => {
    const { fixture } = setup();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the floating header, background map, and bottom sheet together', async () => {
    const { fixture, el } = setup();
    await fixture.whenStable();

    expect(el.textContent).toContain('RideLog'); // header brand
    expect(el.querySelector('app-route-map')).toBeTruthy(); // background map
    expect(el.querySelector('app-bottom-sheet .sheet.half')).toBeTruthy(); // sheet at its default snap
  });
});
