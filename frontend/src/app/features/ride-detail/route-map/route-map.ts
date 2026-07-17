import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { decodePolyline } from './polyline-decoder';

/**
 * The only place Leaflet is used directly, so the map engine can later be swapped for MapLibre
 * (backlog) by replacing this component. Input is an encoded polyline; it draws the track and fits.
 */
@Component({
  selector: 'app-route-map',
  template: `<div #map class="route-map"></div>`,
  styleUrl: './route-map.scss',
})
export class RouteMap implements AfterViewInit, OnDestroy {
  readonly polyline = input<string | null | undefined>();

  private readonly mapElement = viewChild.required<ElementRef<HTMLElement>>('map');

  private map?: L.Map;
  private track?: L.Polyline;

  constructor() {
    // Redraw when the polyline changes (e.g. navigating between rides) once the map exists.
    effect(() => {
      const encoded = this.polyline();
      if (this.map) {
        this.draw(encoded);
      }
    });
  }

  ngAfterViewInit(): void {
    this.map = L.map(this.mapElement().nativeElement);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19,
    }).addTo(this.map);
    this.draw(this.polyline());
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  private draw(encoded: string | null | undefined): void {
    if (!this.map) {
      return;
    }

    this.track?.remove();
    this.track = undefined;

    const coordinates = encoded ? decodePolyline(encoded) : [];
    if (coordinates.length === 0) {
      this.map.setView([0, 0], 2);
      return;
    }

    this.track = L.polyline(coordinates, { color: '#e63946', weight: 4 }).addTo(this.map);
    this.map.fitBounds(this.track.getBounds());
  }
}
