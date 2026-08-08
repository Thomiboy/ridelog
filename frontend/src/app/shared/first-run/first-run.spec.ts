import { TestBed } from '@angular/core/testing';
import { FirstRun } from './first-run';
import { translocoTesting } from '../../core/i18n/transloco-testing';

describe('FirstRun', () => {
  function render(signedIn: boolean) {
    TestBed.configureTestingModule({ imports: [FirstRun, translocoTesting()] });
    const fixture = TestBed.createComponent(FirstRun);
    fixture.componentRef.setInput('ownLog', signedIn);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  /**
   * The one thing a new rider has to be told, because it is the most surprising thing about the
   * product: Polar only delivers what was recorded after linking, so an empty log is not a fault
   * and yesterday's rides are not coming.
   */
  it('says rides start arriving from the link, not before it', () => {
    expect(render(true)).toContain('recorded after you link');
  });

  /**
   * Looking at somebody else's empty log is a different situation: there is nothing to do about it,
   * so it must not read as an instruction.
   */
  it('does not tell a visitor to link anything', () => {
    const visitor = render(false);

    expect(visitor).toContain('No rides here yet');
    expect(visitor).not.toContain('recorded after you link');
  });
});
