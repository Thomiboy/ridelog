import { Component, ElementRef, OnDestroy, effect, inject, input, viewChild } from '@angular/core';
import type * as L from 'leaflet';
import { createRouteMap, drawRestStops, drawRoutes, setTileLayer, shouldFitView } from './leaflet-map';
import type { RestStop } from '../../../core/api/ride.models';
import { ThemeService } from '../../../core/theme/theme.service';

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
  /** Rest markers for a single displayed route (ride detail). Ignored for coverage/multi-route maps. */
  readonly restStops = input<RestStop[]>([]);

  private readonly host = viewChild<ElementRef<HTMLElement>>('map');
  private readonly theme = inject(ThemeService);

  private map?: L.Map;
  private tile?: L.TileLayer;
  private layers: L.Layer[] = [];

  constructor() {
    // Swap the basemap tiles when the theme changes (light OSM ⇄ dark CARTO).
    effect(() => {
      const theme = this.theme.resolved();
      if (this.map) {
        this.tile?.remove();
        this.tile = setTileLayer(this.map, theme);
      }
    });

    effect(() => {
      const host = this.host();
      const routes = this.routes();
      const obscuredBottomFraction = this.obscuredBottomFraction();
      const coverage = this.coverage();
      const restStops = this.restStops();
      if (!host) {
        return;
      }
      const created = !this.map;
      const map = (this.map ??= createRouteMap(host.nativeElement));
      if (created) {
        this.tile = setTileLayer(map, this.theme.resolved());
      }
      this.layers.forEach((layer) => layer.remove());
      const bottomPaddingPx = obscuredBottomFraction * map.getSize().y;
      const fit = shouldFitView(created, obscuredBottomFraction);
      const layers = drawRoutes(map, routes, undefined, { bottomPaddingPx, coverage, fit });
      // Rest markers belong to a single highlighted route, not the coverage or multi-route views.
      if (!coverage && routes.length === 1 && restStops.length > 0) {
        layers.push(...drawRestStops(map, restStops));
      }
      this.layers = layers;
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }
}
