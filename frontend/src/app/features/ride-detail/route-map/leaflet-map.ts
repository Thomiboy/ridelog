import * as Leaflet from 'leaflet';
import { decodePolyline } from './polyline-decoder';
import type { RestStop } from '../../../core/api/ride.models';
import type { Theme } from '../../../core/theme/theme.service';

/** Free basemaps per theme: OSM standard for light, CARTO dark for dark. */
const TILE_LAYERS: Record<Theme, { url: string; attribution: string }> = {
  light: {
    url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '© OpenStreetMap contributors',
  },
  dark: {
    url: 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png',
    attribution: '© OpenStreetMap contributors, © CARTO',
  },
};

/** The slice of the Leaflet API we use — injectable so tests pass a fake without module mocking. */
export type LeafletApi = Pick<
  typeof Leaflet,
  'map' | 'tileLayer' | 'polyline' | 'latLngBounds' | 'marker' | 'divIcon'
>;

/**
 * Distinct track colours, in draw order. The first is the app's navy (matching the Material accent
 * and the owner's bike), so single-route views look unchanged; the rest distinguish the Statistics
 * page's longest routes.
 */
export const ROUTE_COLORS = ['#1b3a6b', '#c2410c', '#0f766e'] as const;

/** How the routes are drawn and the view fitted. */
export interface DrawOptions {
  /**
   * Pixels at the bottom of the map that are covered by the content sheet. The view is fitted into
   * the area above it, so the whole route stays visible on a half-open page.
   */
  bottomPaddingPx?: number;

  /**
   * Coverage map: every route in one translucent colour (so frequently-ridden roads darken) with no
   * start/finish markers — the "where have I been" overview, rather than a single highlighted ride.
   */
  coverage?: boolean;

  /**
   * Whether to fit the view to the routes. Defaults to true; pass false to keep the current view —
   * used when the content sheet is fully open, where fitting into the tiny visible strip would shrink
   * the route to a meaningless dot.
   */
  fit?: boolean;
}

/** Breathing room around the fitted routes so tracks aren't flush against the map edges. */
const EDGE_PADDING_PX = 24;

/**
 * Above this obscured fraction the content sheet is essentially full and the visible strip is too
 * small to fit into — routes would shrink to a meaningless sliver.
 */
const FULL_OBSCURED_THRESHOLD = 0.85;

/**
 * Whether to fit the view to the routes: always on the first draw, and afterwards only while the
 * sheet leaves enough of the map visible. Changing the displayed routes deliberately does *not*
 * force a fit — opening Rides swaps the background to every route beneath a full-height calendar,
 * and dragging the sheet down fits them once there's room.
 */
export function shouldFitView(created: boolean, obscuredBottomFraction: number): boolean {
  return created || obscuredBottomFraction < FULL_OBSCURED_THRESHOLD;
}

/** Track opacity for the coverage overview, so overlapping routes visibly build up. */
const COVERAGE_OPACITY = 0.35;

/** Creates a Leaflet map on the element (add a tile layer with setTileLayer). */
export function createRouteMap(element: HTMLElement, api: LeafletApi = Leaflet): Leaflet.Map {
  return api.map(element);
}

/** Adds (and returns) the basemap tile layer for the given theme, so the caller can swap it later. */
export function setTileLayer(map: Leaflet.Map, theme: Theme, api: LeafletApi = Leaflet): Leaflet.TileLayer {
  const { url, attribution } = TILE_LAYERS[theme];
  return api.tileLayer(url, { attribution, maxZoom: 19 }).addTo(map);
}

/**
 * Draws each encoded route in a distinct colour and fits the view to all of them. When exactly one
 * route is shown (ride detail, single-route background), it also marks the start and finish. Returns
 * every drawn layer (tracks and markers), empty when there's nothing to draw, so the caller can
 * remove them before redrawing.
 */
export function drawRoutes(
  map: Leaflet.Map,
  encoded: readonly (string | null | undefined)[],
  api: LeafletApi = Leaflet,
  options: DrawOptions = {},
): Leaflet.Layer[] {
  const routes = encoded.map((e) => (e ? decodePolyline(e) : [])).filter((coords) => coords.length > 0);
  if (routes.length === 0) {
    map.setView([0, 0], 2);
    return [];
  }

  const tracks = routes.map((coords, index) =>
    api.polyline(coords, coverageStyle(index, options.coverage ?? false)).addTo(map),
  );
  if (options.fit ?? true) {
    map.fitBounds(api.latLngBounds(routes.flat()), {
      paddingTopLeft: [EDGE_PADDING_PX, EDGE_PADDING_PX],
      paddingBottomRight: [EDGE_PADDING_PX, EDGE_PADDING_PX + (options.bottomPaddingPx ?? 0)],
    });
  }

  // Coverage is a single translucent layer of every route — no per-ride start/finish markers. They
  // also only make sense for a single track; several distinct routes (Statistics) stay clean too.
  const markers = !options.coverage && routes.length === 1 ? drawStartFinishMarkers(map, routes[0], api) : [];
  return [...tracks, ...markers];
}

/** Per-route line style: one translucent colour for coverage, otherwise the distinct palette. */
function coverageStyle(index: number, coverage: boolean): Leaflet.PolylineOptions {
  return coverage
    ? { color: ROUTE_COLORS[0], weight: 3, opacity: COVERAGE_OPACITY }
    : { color: ROUTE_COLORS[index % ROUTE_COLORS.length], weight: 4 };
}

/** A round dot (start) / square (finish) divIcon — inline-styled so no external assets or CSS class. */
function endpointIcon(api: LeafletApi, color: string, radius: string): Leaflet.DivIcon {
  return api.divIcon({
    className: '', // drop Leaflet's default white box so only the styled dot shows
    html:
      `<span style="display:block;width:14px;height:14px;border-radius:${radius};` +
      `background:${color};border:2px solid #fff;box-shadow:0 0 0 1px rgba(0,0,0,.35)"></span>`,
    iconSize: [18, 18],
    iconAnchor: [9, 9],
  });
}

/** Draws an amber pause marker at each rest stop; returns them so the caller can remove them on redraw. */
export function drawRestStops(map: Leaflet.Map, restStops: readonly RestStop[], api: LeafletApi = Leaflet): Leaflet.Marker[] {
  const icon = api.divIcon({
    className: '',
    html:
      '<span style="display:flex;align-items:center;justify-content:center;width:16px;height:16px;' +
      'border-radius:50%;background:#f59e0b;border:2px solid #fff;box-shadow:0 0 0 1px rgba(0,0,0,.35)">' +
      '<span style="width:2px;height:6px;background:#fff;margin:0 1px"></span>' +
      '<span style="width:2px;height:6px;background:#fff;margin:0 1px"></span></span>',
    iconSize: [20, 20],
    iconAnchor: [10, 10],
  });
  return restStops.map((rest) => api.marker([rest.latitude, rest.longitude], { icon, title: 'Rest' }).addTo(map));
}

/** Places a green start marker and a red finish marker at the ends of the route (finish on top). */
function drawStartFinishMarkers(map: Leaflet.Map, coords: [number, number][], api: LeafletApi): Leaflet.Marker[] {
  const start = api.marker(coords[0], { icon: endpointIcon(api, '#2e7d32', '50%'), title: 'Start' }).addTo(map);
  const finish = api
    .marker(coords[coords.length - 1], { icon: endpointIcon(api, '#c62828', '3px'), title: 'Finish' })
    .addTo(map);
  return [start, finish];
}
