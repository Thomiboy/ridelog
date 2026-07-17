import { Component, ElementRef, OnDestroy, effect, input, viewChild } from '@angular/core';
import type * as L from 'leaflet';
import { createRouteMap, drawRoute } from './leaflet-map';

/**
 * The only place Leaflet is used (via leaflet-map helpers), so the map engine can later be swapped
 * for MapLibre (backlog) by replacing this component. Input is an encoded polyline; it draws the
 * track and fits. Wiring runs in an effect keyed on the view child and input — no lifecycle-timing
 * dependence (which is racy under zoneless change detection).
 */
@Component({
  selector: 'app-route-map',
  template: `<div #map class="route-map"></div>`,
  styleUrl: './route-map.scss',
})
export class RouteMap implements OnDestroy {
  readonly polyline = input<string | null | undefined>();

  private readonly host = viewChild<ElementRef<HTMLElement>>('map');

  private map?: L.Map;
  private track?: L.Polyline;

  constructor() {
    effect(() => {
      const host = this.host();
      const encoded = this.polyline();
      if (!host) {
        return;
      }
      this.map ??= createRouteMap(host.nativeElement);
      this.track?.remove();
      this.track = drawRoute(this.map, encoded) ?? undefined;
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }
}
