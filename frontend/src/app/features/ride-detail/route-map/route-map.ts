import { Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import type * as L from 'leaflet';
import { createRouteMap, drawRoutes } from './leaflet-map';

/**
 * The only place Leaflet is used (via leaflet-map helpers), so the map engine can later be swapped
 * for MapLibre (backlog) by replacing this component. Input is a list of encoded polylines; it draws
 * each in a distinct colour and fits. Wiring runs in an effect keyed on the view child and input — no
 * lifecycle-timing dependence (which is racy under zoneless change detection).
 */
@Component({
  selector: 'app-route-map',
  template: `<div #map class="route-map"></div>`,
  styleUrl: './route-map.scss',
})
export class RouteMap implements OnDestroy {
  readonly routes = input<string[]>([]);

  private readonly host = viewChild<ElementRef<HTMLElement>>('map');

  private map?: L.Map;
  private tracks: L.Polyline[] = [];

  constructor() {
    effect(() => {
      const host = this.host();
      const routes = this.routes();
      if (!host) {
        return;
      }
      this.map ??= createRouteMap(host.nativeElement);
      this.tracks.forEach((track) => track.remove());
      this.tracks = drawRoutes(this.map, routes);
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }
}
