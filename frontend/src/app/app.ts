import { Component, computed, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './layout/header/header';
import { BottomSheet } from './layout/bottom-sheet/bottom-sheet';
import { SheetState } from './layout/bottom-sheet/sheet-state';
import { SNAP_HEIGHTS } from './layout/bottom-sheet/snap';
import { MapState } from './core/map/map-state';
import { RouteMap } from './features/ride-detail/route-map/route-map';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header, BottomSheet, RouteMap],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly mapState = inject(MapState);
  private readonly sheetState = inject(SheetState);

  /** How much of the background map the content sheet currently covers, so routes fit above it. */
  protected readonly obscuredBottomFraction = computed(() => SNAP_HEIGHTS[this.sheetState.current()]);

  constructor() {
    this.mapState.loadLatest();
  }
}
