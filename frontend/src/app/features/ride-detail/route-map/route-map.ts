import { Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import type * as L from 'leaflet';
import { createRouteMap, drawRoutes } from './leaflet-map';

/**
 * The only place Leaflet is used (via leaflet-map helpers), so the map engine can later be swapped
 * for MapLibre (backlog) by replacing this component. Input is a list of encoded polylines; it draws
 * each in a distinct colour and fits. `obscuredBottomFraction` is how much of the map the content
 * sheet covers (0–1), so the view fits the route into the visible area above it. Wiring runs in an
 * effect keyed on the view child and inputs — no lifecycle-timing dependence (racy when zoneless).
 */
@Component({
  selector: 'app-route-map',
  template: `<div #map class="route-map"></div>`,
  styleUrl: './route-map.scss',
})
export class RouteMap implements OnDestroy {
  readonly routes = input<string[]>([]);
  readonly obscuredBottomFraction = input<number>(0);
  /** Draw all routes as one translucent coverage layer (Rides "all routes" map) instead of highlighting one. */
  readonly coverage = input<boolean>(false);

  private readonly host = viewChild<ElementRef<HTMLElement>>('map');

  private map?: L.Map;
  private layers: L.Layer[] = [];

  constructor() {
    effect(() => {
      const host = this.host();
      const routes = this.routes();
      const obscuredBottomFraction = this.obscuredBottomFraction();
      const coverage = this.coverage();
      if (!host) {
        return;
      }
      const map = (this.map ??= createRouteMap(host.nativeElement));
      this.layers.forEach((layer) => layer.remove());
      const bottomPaddingPx = obscuredBottomFraction * map.getSize().y;
      this.layers = drawRoutes(map, routes, undefined, { bottomPaddingPx, coverage });
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }
}
