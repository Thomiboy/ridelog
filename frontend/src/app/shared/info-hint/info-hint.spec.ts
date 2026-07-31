import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { InfoHint } from './info-hint';

@Component({
  imports: [InfoHint],
  template: `<app-info-hint key="rideDetail.temperatureNote" />`,
})
class Host {}

describe('InfoHint', () => {
  function render() {
    TestBed.configureTestingModule({
      imports: [Host],
      providers: [provideTransloco({ config: { availableLangs: ['en'], defaultLang: 'en' } })],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  // The caveat only exists behind a hover, so without a label the icon says nothing at all to
  // anyone not using a mouse — which is the one case where the hint matters most.
  it('carries the caveat as an accessible label, not only as a tooltip', () => {
    const icon = render().querySelector('[data-info-hint]');

    expect(icon).not.toBeNull();
    expect(icon!.getAttribute('aria-label')).toBeTruthy();
  });
});
