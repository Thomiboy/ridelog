import { vi, type Mock } from 'vitest';
import { createRouteMap, drawRoutes, ROUTE_COLORS, type LeafletApi } from './leaflet-map';

const ENCODED = '_p~iF~ps|U_ulLnnqC_mqNvxq`@';

function fakeLeaflet() {
  const line: Record<string, unknown> = { getBounds: vi.fn(() => 'BOUNDS'), remove: vi.fn() };
  line['addTo'] = vi.fn(() => line);
  const map = { setView: vi.fn(), fitBounds: vi.fn(), remove: vi.fn() };
  const api = {
    map: vi.fn(() => map),
    tileLayer: vi.fn(() => ({ addTo: vi.fn() })),
    polyline: vi.fn(() => line),
    latLngBounds: vi.fn(() => 'ALL_BOUNDS'),
  } as unknown as LeafletApi;
  return { api, map };
}

describe('leaflet-map', () => {
  it('creates a map with an OpenStreetMap tile layer', () => {
    const { api } = fakeLeaflet();

    createRouteMap(document.createElement('div'), api);

    expect(api.tileLayer).toHaveBeenCalled();
    const options = (api.tileLayer as unknown as Mock).mock.calls[0][1] as { attribution: string };
    expect(options.attribution).toContain('OpenStreetMap');
  });

  it('draws a single decoded route and fits the map to its bounds', () => {
    const { api, map } = fakeLeaflet();

    const tracks = drawRoutes(map as never, [ENCODED], api);

    expect(api.polyline).toHaveBeenCalledTimes(1);
    const coords = (api.polyline as unknown as Mock).mock.calls[0][0] as [number, number][];
    expect(coords[0][0]).toBeCloseTo(38.5, 4);
    expect(coords[0][1]).toBeCloseTo(-120.2, 4);
    expect((map.fitBounds as unknown as Mock).mock.calls[0][0]).toBe('ALL_BOUNDS');
    expect(tracks).toHaveLength(1);
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

  it('draws nothing and shows the default view when there are no routes', () => {
    const { api, map } = fakeLeaflet();

    const tracks = drawRoutes(map as never, [], api);

    expect(api.polyline).not.toHaveBeenCalled();
    expect(map.setView).toHaveBeenCalled();
    expect(tracks).toEqual([]);
  });
});
