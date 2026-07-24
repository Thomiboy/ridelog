import * as Leaflet from 'leaflet';
import { decodePolyline } from './polyline-decoder';

/** The slice of the Leaflet API we use — injectable so tests pass a fake without module mocking. */
export type LeafletApi = Pick<typeof Leaflet, 'map' | 'tileLayer' | 'polyline' | 'latLngBounds'>;

/**
 * Distinct track colours, in draw order. The first is the app's navy (matching the Material accent
 * and the owner's bike), so single-route views look unchanged; the rest distinguish the Statistics
 * page's longest routes.
 */
export const ROUTE_COLORS = ['#1b3a6b', '#c2410c', '#0f766e'] as const;

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

/**
 * Draws each encoded route in a distinct colour and fits the view to all of them; returns the drawn
 * tracks (empty when there's nothing to draw) so the caller can remove them before redrawing.
 */
export function drawRoutes(
  map: Leaflet.Map,
  encoded: readonly (string | null | undefined)[],
  api: LeafletApi = Leaflet,
): Leaflet.Polyline[] {
  const routes = encoded.map((e) => (e ? decodePolyline(e) : [])).filter((coords) => coords.length > 0);
  if (routes.length === 0) {
    map.setView([0, 0], 2);
    return [];
  }

  const tracks = routes.map((coords, index) =>
    api.polyline(coords, { color: ROUTE_COLORS[index % ROUTE_COLORS.length], weight: 4 }).addTo(map),
  );
  map.fitBounds(api.latLngBounds(routes.flat()));
  return tracks;
}
