import * as Leaflet from 'leaflet';
import { decodePolyline } from './polyline-decoder';

/** The slice of the Leaflet API we use — injectable so tests pass a fake without module mocking. */
export type LeafletApi = Pick<typeof Leaflet, 'map' | 'tileLayer' | 'polyline'>;

/** Creates a Leaflet map on the element with an OpenStreetMap tile layer. */
export function createRouteMap(element: HTMLElement, api: LeafletApi = Leaflet): Leaflet.Map {
  const map = api.map(element);
  api
    .tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19,
    })
    .addTo(map);
  return map;
}

/** Draws the decoded route on the map and fits the view to it; returns the drawn track (null if empty). */
export function drawRoute(
  map: Leaflet.Map,
  encoded: string | null | undefined,
  api: LeafletApi = Leaflet,
): Leaflet.Polyline | null {
  const coordinates = encoded ? decodePolyline(encoded) : [];
  if (coordinates.length === 0) {
    map.setView([0, 0], 2);
    return null;
  }

  // Navy, matching the app's Material accent (and the owner's bike).
  const track = api.polyline(coordinates, { color: '#1b3a6b', weight: 4 }).addTo(map);
  map.fitBounds(track.getBounds());
  return track;
}
