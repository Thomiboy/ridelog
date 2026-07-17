import { vi, type Mock } from 'vitest';
import { createRouteMap, drawRoute, type LeafletApi } from './leaflet-map';

function fakeLeaflet() {
  const line: Record<string, unknown> = { getBounds: vi.fn(() => 'BOUNDS'), remove: vi.fn() };
  line['addTo'] = vi.fn(() => line);
  const map = { setView: vi.fn(), fitBounds: vi.fn(), remove: vi.fn() };
  const api = {
    map: vi.fn(() => map),
    tileLayer: vi.fn(() => ({ addTo: vi.fn() })),
    polyline: vi.fn(() => line),
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

  it('draws the decoded route and fits the map to its bounds', () => {
    const { api, map } = fakeLeaflet();

    drawRoute(map as never, '_p~iF~ps|U_ulLnnqC_mqNvxq`@', api);

    expect(api.polyline).toHaveBeenCalled();
    const coords = (api.polyline as unknown as Mock).mock.calls[0][0] as [number, number][];
    expect(coords[0][0]).toBeCloseTo(38.5, 4);
    expect(coords[0][1]).toBeCloseTo(-120.2, 4);
    expect(map.fitBounds).toHaveBeenCalledWith('BOUNDS');
  });

  it('does not draw a route when there is no polyline', () => {
    const { api, map } = fakeLeaflet();

    drawRoute(map as never, null, api);

    expect(api.polyline).not.toHaveBeenCalled();
    expect(map.setView).toHaveBeenCalled();
  });
});
