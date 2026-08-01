import { vi, type Mock } from 'vitest';
import {
  createRouteMap,
  drawHighlights,
  drawRestStops,
  drawRoutes,
  ROUTE_COLORS,
  setTileLayer,
  shouldFitView,
  watchPointer,
  type LeafletApi,
} from './leaflet-map';
import type { PointerOnMap } from '../../../core/map/map-state';

const ENCODED = '_p~iF~ps|U_ulLnnqC_mqNvxq`@';

function fakeLeaflet() {
  const line: Record<string, unknown> = { getBounds: vi.fn(() => 'BOUNDS'), remove: vi.fn() };
  line['addTo'] = vi.fn(() => line);
  const makeMarker = () => {
    const marker: Record<string, unknown> = { remove: vi.fn() };
    marker['addTo'] = vi.fn(() => marker);
    return marker;
  };
  const map = {
    setView: vi.fn(),
    fitBounds: vi.fn(),
    remove: vi.fn(),
    on: vi.fn(),
    off: vi.fn(),
    // 100 container pixels span 250 m at this pretend zoom, so a pixel is 2.5 m.
    containerPointToLatLng: vi.fn((point: [number, number]) => ({ lat: 0, lng: point[0] })),
    distance: vi.fn(() => 250),
  };
  const makeTile = () => {
    const tile: Record<string, unknown> = { remove: vi.fn() };
    tile['addTo'] = vi.fn(() => tile);
    return tile;
  };
  const api = {
    map: vi.fn(() => map),
    tileLayer: vi.fn(() => makeTile()),
    polyline: vi.fn(() => line),
    latLngBounds: vi.fn(() => 'ALL_BOUNDS'),
    marker: vi.fn(() => makeMarker()),
    divIcon: vi.fn((options) => options),
  } as unknown as LeafletApi;
  return { api, map };
}

describe('leaflet-map', () => {
  it('creates a Leaflet map on the element', () => {
    const { api } = fakeLeaflet();

    createRouteMap(document.createElement('div'), api);

    expect(api.map).toHaveBeenCalled();
  });

  it('uses OSM tiles in light mode and a dark basemap in dark mode', () => {
    const { api, map } = fakeLeaflet();

    setTileLayer(map as never, 'light', api);
    expect((api.tileLayer as unknown as Mock).mock.calls[0][0]).toContain('openstreetmap');

    setTileLayer(map as never, 'dark', api);
    expect((api.tileLayer as unknown as Mock).mock.calls[1][0]).toContain('cartocdn');
  });

  it('draws a single decoded route and fits the map to its bounds', () => {
    const { api, map } = fakeLeaflet();

    const tracks = drawRoutes(map as never, [ENCODED], api);

    expect(api.polyline).toHaveBeenCalledTimes(1);
    const coords = (api.polyline as unknown as Mock).mock.calls[0][0] as [number, number][];
    expect(coords[0][0]).toBeCloseTo(38.5, 4);
    expect(coords[0][1]).toBeCloseTo(-120.2, 4);
    expect((map.fitBounds as unknown as Mock).mock.calls[0][0]).toBe('ALL_BOUNDS');
    expect(tracks.length).toBeGreaterThanOrEqual(1);
  });

  it('marks the start and finish at the ends of a single route', () => {
    const { api, map } = fakeLeaflet();

    const layers = drawRoutes(map as never, [ENCODED], api);

    // Two markers placed at the first and last decoded coordinate of the one route.
    expect(api.marker).toHaveBeenCalledTimes(2);
    const start = (api.marker as unknown as Mock).mock.calls[0][0] as [number, number];
    const finish = (api.marker as unknown as Mock).mock.calls[1][0] as [number, number];
    expect(start[0]).toBeCloseTo(38.5, 4);
    expect(start[1]).toBeCloseTo(-120.2, 4);
    expect(finish[0]).toBeCloseTo(43.252, 4);
    expect(finish[1]).toBeCloseTo(-126.453, 4);
    // Returned so the caller can remove them on redraw: 1 track + 2 markers.
    expect(layers).toHaveLength(3);
  });

  it('does not mark start/finish when several routes are drawn', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED, ENCODED], api);

    expect(api.marker).not.toHaveBeenCalled();
  });

  it('coverage mode draws a translucent track and no markers, even for a single route', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED], api, { coverage: true });

    const options = (api.polyline as unknown as Mock).mock.calls[0][1] as { opacity?: number };
    expect(options.opacity).toBeLessThan(1); // translucent so overlapping routes darken (coverage feel)
    expect(api.marker).not.toHaveBeenCalled(); // a coverage map has no per-ride start/finish
  });

  it('draws a marker at each rest stop', () => {
    const { api, map } = fakeLeaflet();

    const markers = drawRestStops(map as never, [{ latitude: 1, longitude: 2 }, { latitude: 3, longitude: 4 }], api);

    expect(api.marker).toHaveBeenCalledTimes(2);
    expect((api.marker as unknown as Mock).mock.calls[0][0]).toEqual([1, 2]);
    expect(markers).toHaveLength(2);
  });

  // The highlight says "the rider was here" for the point being hovered on the graph. In a
  // comparison there is one per route, and each takes its own route's colour so the eye can tell
  // which ride it belongs to without a legend.
  it('draws one highlight per position, in that route\'s colour', () => {
    const { api, map } = fakeLeaflet();

    const markers = drawHighlights(map as never, [[1, 2], [3, 4]], api);

    expect(api.marker).toHaveBeenCalledTimes(2);
    expect((api.marker as unknown as Mock).mock.calls[0][0]).toEqual([1, 2]);
    expect((api.divIcon as unknown as Mock).mock.calls[0][0].html).toContain(ROUTE_COLORS[0]);
    expect((api.divIcon as unknown as Mock).mock.calls[1][0].html).toContain(ROUTE_COLORS[1]);
    expect(markers).toHaveLength(2);
  });

  it('coverage mode draws every route in the same colour', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED, ENCODED, ENCODED], api, { coverage: true });

    const colours = (api.polyline as unknown as Mock).mock.calls.map((c) => (c[1] as { color: string }).color);
    expect(new Set(colours).size).toBe(1);
  });

  it('reserves space at the bottom so the route clears the content sheet', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED], api, { bottomPaddingPx: 320 });

    const options = (map.fitBounds as unknown as Mock).mock.calls[0][1] as { paddingBottomRight: [number, number] };
    // The route fits into the area above the sheet: bottom padding covers the obscured height.
    expect(options.paddingBottomRight[1]).toBeGreaterThanOrEqual(320);
  });

  it('draws each route in a distinct colour, the first in the default navy', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED, ENCODED, ENCODED], api);

    const colours = (api.polyline as unknown as Mock).mock.calls.map((c) => (c[1] as { color: string }).color);
    expect(colours).toHaveLength(3);
    expect(colours[0]).toBe(ROUTE_COLORS[0]); // single-route look preserved
    expect(new Set(colours).size).toBe(3); // all distinct
  });

  it('fits the map to the bounds spanning every route', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED, ENCODED], api);

    // Bounds are built from the union of all routes' coordinates, then the map fits to them once.
    expect(api.latLngBounds).toHaveBeenCalledTimes(1);
    const union = (api.latLngBounds as unknown as Mock).mock.calls[0][0] as unknown[];
    expect(union.length).toBeGreaterThan(2); // more points than a single route contributes
    expect(map.fitBounds).toHaveBeenCalledTimes(1);
  });

  it('fits on the first draw no matter how much the sheet covers', () => {
    expect(shouldFitView(true, 0.92)).toBe(true);
  });

  it('fits whenever enough of the map is visible', () => {
    expect(shouldFitView(false, 0.55)).toBe(true);
  });

  it('keeps the view once the sheet is essentially full, rather than squeezing routes into a sliver', () => {
    // Opening Rides swaps the background to every route while the calendar covers 92% — refitting
    // there would cram them into the remaining strip. Dragging the sheet down fits them properly.
    expect(shouldFitView(false, 0.92)).toBe(false);
  });

  it('still draws the tracks but skips fitting the view when fit is false', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [ENCODED], api, { fit: false });

    expect(api.polyline).toHaveBeenCalledTimes(1); // route is drawn
    expect(map.fitBounds).not.toHaveBeenCalled(); // but the current view is kept
  });

  it('draws nothing and shows the default view when there are no routes', () => {
    const { api, map } = fakeLeaflet();

    const tracks = drawRoutes(map as never, [], api);

    expect(api.polyline).not.toHaveBeenCalled();
    expect(api.marker).not.toHaveBeenCalled();
    expect(map.setView).toHaveBeenCalled();
    expect(tracks).toEqual([]);
  });

  it('keeps the current view when there is nothing to draw and fitting is off', () => {
    const { api, map } = fakeLeaflet();

    drawRoutes(map as never, [], api, { fit: false });

    // Reframing to the world view is still a reframe: with the sheet essentially full the view has
    // to be left alone, or a transient empty state (e.g. mid-navigation) throws it away.
    expect(map.setView).not.toHaveBeenCalled();
  });

  // Pointing at the map has to reach the graph, and the only thing that knows where the pointer is
  // is the map instance. Reported as a plain pair so nothing downstream has to know about Leaflet.
  it('reports where the pointer is over the map, and when it leaves', () => {
    const { map } = fakeLeaflet();
    const seen: (PointerOnMap | null)[] = [];

    watchPointer(map as never, (position) => seen.push(position));

    const handlers = Object.fromEntries((map.on as unknown as Mock).mock.calls);
    handlers['mousemove']({ latlng: { lat: 47.5, lng: 19.04 } });
    handlers['mouseout']();

    expect(seen).toEqual([{ position: [47.5, 19.04], metresPerPixel: 2.5 }, null]);
  });
});
