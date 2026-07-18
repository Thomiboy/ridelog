import { Component, effect, inject, signal } from '@angular/core';
import { SNAP_HEIGHTS, SnapState, chooseSnap } from './snap';
import { SheetState } from './sheet-state';

/**
 * The draggable content sheet at the bottom of the shell: rounded top corners, three snap
 * points (collapsed / half / full), starting at half. Dragging the handle resizes the sheet
 * and releases into the nearest snap state.
 */
@Component({
  selector: 'app-bottom-sheet',
  templateUrl: './bottom-sheet.html',
  styleUrl: './bottom-sheet.scss',
})
export class BottomSheet {
  private readonly sheetState = inject(SheetState);

  readonly state = signal<SnapState>('half');

  /** Transient height (viewport fraction) while dragging; null when settled on a snap point. */
  readonly dragHeight = signal<number | null>(null);

  private dragging = false;

  constructor() {
    // Pages can request a snap (e.g. ride selection → half); apply and consume it.
    effect(() => {
      const requested = this.sheetState.requested();
      if (requested !== null) {
        this.snapTo(requested);
        this.sheetState.clear();
      }
    });
  }

  snapTo(state: SnapState): void {
    this.dragHeight.set(null);
    this.state.set(state);
  }

  get heightStyle(): string {
    const fraction = this.dragHeight() ?? SNAP_HEIGHTS[this.state()];
    return `${fraction * 100}dvh`;
  }

  onHandlePointerDown(event: PointerEvent): void {
    this.dragging = true;
    (event.target as HTMLElement).setPointerCapture(event.pointerId);
  }

  onHandlePointerMove(event: PointerEvent): void {
    if (!this.dragging) {
      return;
    }
    const fraction = 1 - event.clientY / window.innerHeight;
    this.dragHeight.set(Math.min(Math.max(fraction, 0.05), 0.95));
  }

  onHandlePointerUp(): void {
    if (!this.dragging) {
      return;
    }
    this.dragging = false;
    const fraction = this.dragHeight();
    this.snapTo(fraction === null ? this.state() : chooseSnap(fraction));
  }
}
