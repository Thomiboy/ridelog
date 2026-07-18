import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './layout/header/header';
import { BottomSheet } from './layout/bottom-sheet/bottom-sheet';
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

  constructor() {
    this.mapState.loadLatest();
  }
}
