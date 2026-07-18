import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BottomSheet } from './bottom-sheet';

@Component({
  imports: [BottomSheet],
  template: `<app-bottom-sheet><p class="inner">Hello sheet</p></app-bottom-sheet>`,
})
class Host {}

describe('BottomSheet', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [Host] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const sheet = fixture.debugElement.children[0].componentInstance as BottomSheet;
    return { fixture, el, sheet };
  }

  it('projects its content', () => {
    const { el } = setup();

    expect(el.querySelector('.inner')?.textContent).toContain('Hello sheet');
  });

  it('starts at the half snap point', () => {
    const { el, sheet } = setup();

    expect(sheet.state()).toBe('half');
    expect(el.querySelector('.sheet')?.classList).toContain('half');
  });

  it('snaps to full and back to collapsed', () => {
    const { fixture, el, sheet } = setup();

    sheet.snapTo('full');
    fixture.detectChanges();
    expect(el.querySelector('.sheet')?.classList).toContain('full');

    sheet.snapTo('collapsed');
    fixture.detectChanges();
    expect(el.querySelector('.sheet')?.classList).toContain('collapsed');
  });
});
